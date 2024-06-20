using System.Text;
using System.Text.Json;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using StackExchange.Redis;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TicketReservation.Room;

public class Function
{
	private static readonly string redisConnectionString;
	private static readonly string websocketApiServiceUrl;
	private static readonly ConnectionMultiplexer redis;
	private static readonly IDatabase db;
	private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

	static Function()
	{
		string redisAddress = GetEnvironmentVariable("ELASTICACHE_ENDPOINT_ADDRESS");
		string redisPort = GetEnvironmentVariable("ELASTICACHE_ENDPOINT_PORT");

		// SSL is necessary when "encrypt data in transit" is enabled for ElastiCache
		redisConnectionString = $"{redisAddress}:{redisPort},ssl=True";
		redis = ConnectionMultiplexer.Connect(redisConnectionString);
		db = redis.GetDatabase();

		string stageName = GetEnvironmentVariable("STAGE_NAME");
		string websocketApiGatewayEndpoint = GetEnvironmentVariable("WEBSOCKET_GATEWAY_API_ENDPOINT");

		// Omit trailing slash, otherwise library throws error
		websocketApiServiceUrl = $"{websocketApiGatewayEndpoint.Replace("wss", "https")}/{stageName}";
	}

	private static string GetEnvironmentVariable(string name)
	{
		return Environment.GetEnvironmentVariable(name) ??
			throw new InvalidOperationException($"{name} not provided");
	}

	public static async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string eventType = request.RequestContext.EventType;

		return eventType switch
		{
			"CONNECT" => await OnConnectAsync(request, context),
			"DISCONNECT" => await OnDisconnectAsync(request, context),
			"MESSAGE" => await OnMessageAsync(request, context),
			_ => throw new NotImplementedException($"Unknown eventType: {eventType}"),
		};
	}

	private static async Task<APIGatewayProxyResponse> OnConnectAsync(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string userId = request.QueryStringParameters["userId"];
		string eventId = "1"; // request.QueryStringParameters["eventId"];
		string connectionId = request.RequestContext.ConnectionId;

		HashEntry[] connectionProperties =
		[
			new("userId", userId),
			new("eventId", eventId),
		];
		await db.HashSetAsync($"connections:{connectionId}", connectionProperties);

		await db.SetAddAsync($"event:{eventId}:connections", connectionId);

		return new APIGatewayProxyResponse { StatusCode = 200 };
	}

	private static async Task<APIGatewayProxyResponse> OnDisconnectAsync(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string connectionId = request.RequestContext.ConnectionId;

		var (_, eventId) = await GetConnectionPropertiesAsync(request.RequestContext.ConnectionId);

		await db.KeyDeleteAsync($"connections:{connectionId}");
		await db.SetRemoveAsync($"event:{eventId}:connections", connectionId);

		return new APIGatewayProxyResponse { StatusCode = 200 };
	}

	private static async Task<APIGatewayProxyResponse> OnMessageAsync(APIGatewayProxyRequest request, ILambdaContext context)
	{
		var (userId, eventId) = await GetConnectionPropertiesAsync(request.RequestContext.ConnectionId);

		Message message = JsonSerializer.Deserialize<Message>(request.Body, jsonSerializerOptions)!;
		if (message.Action == "select")
		{
			bool wasSet = await db.HashSetAsync($"event:{eventId}:seats", $"{message.Row}_{message.Col}", userId, when: When.NotExists);
			if (!wasSet)
			{
				return new APIGatewayProxyResponse { StatusCode = 400, Body = "Already selected" };
			}
		}
		else if (message.Action == "deselect")
		{
			string? currentHolder = await db.HashGetAsync($"event:{eventId}:seats", $"{message.Row}_{message.Col}");
			if (userId != currentHolder)
			{
				return new APIGatewayProxyResponse { StatusCode = 400, Body = "Not selected by you" };
			}

			await db.HashDeleteAsync($"event:{eventId}:seats", $"{message.Row}_{message.Col}");
		}

		List<string> connectionIds = await GetConnectionIdsByEventIdAsync(eventId);
		connectionIds.Remove(request.RequestContext.ConnectionId);

		await PostEventToConnectionsAsync(request, connectionIds);

		return new APIGatewayProxyResponse { StatusCode = 200 };
	}

	private static async Task<(string UserId, string EventId)> GetConnectionPropertiesAsync(string connectionId)
	{
		RedisValue[] connectionProperties = await db.HashGetAsync($"connections:{connectionId}", ["userId", "eventId"]);
		string userId = connectionProperties[0]!;
		string eventId = connectionProperties[1]!;
		return (userId, eventId);
	}

	private static async Task<List<string>> GetConnectionIdsByEventIdAsync(string eventId)
	{
		RedisValue[] connections = await db.SetMembersAsync($"event:{eventId}:connections");
		return connections.Select(c => (string)c!).Where(c => c != null).ToList();
	}

	private static async Task PostEventToConnectionsAsync(APIGatewayProxyRequest request, IEnumerable<string> connectionIds)
	{
		var client = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
		{
			ServiceURL = websocketApiServiceUrl
		});

		List<Exception> exceptions = [];
		foreach (string connectionId in connectionIds)
		{
			try
			{
				var postRequest = new PostToConnectionRequest
				{
					ConnectionId = connectionId,
					Data = new MemoryStream(Encoding.UTF8.GetBytes(request.Body))
				};

				var postResponse = await client.PostToConnectionAsync(postRequest);
			}
			catch (GoneException)
			{
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}

		if (exceptions.Count > 0)
		{
			throw new AggregateException(exceptions);
		}
	}
}

public class Message
{
	public required string Action { get; init; }
	public required string Row { get; init; }
	public required string Col { get; init; }
}
