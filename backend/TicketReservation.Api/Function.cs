using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using StackExchange.Redis;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TicketReservation.Api;

public class Function
{
	private static readonly string tableName;
	private static readonly string stageName;
	private static readonly string redisConnectionString;
	private static readonly ConnectionMultiplexer redisConnection;
	private static readonly IDatabase redisDb;
	private static readonly AmazonDynamoDBClient dynamoDbClient = new();
	private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

	static Function()
	{
		string redisAddress = GetEnvironmentVariable("ELASTICACHE_ENDPOINT_ADDRESS");
		string redisPort = GetEnvironmentVariable("ELASTICACHE_ENDPOINT_PORT");

		// SSL is necessary when "encrypt data in transit" is enabled for ElastiCache
		redisConnectionString = $"{redisAddress}:{redisPort},ssl=True";
		redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
		redisDb = redisConnection.GetDatabase();

		tableName = GetEnvironmentVariable("DYNAMODB_TABLE_NAME");
		stageName = GetEnvironmentVariable("STAGE_NAME");
	}

	private static string GetEnvironmentVariable(string name)
	{
		return Environment.GetEnvironmentVariable(name) ??
			throw new InvalidOperationException($"{name} not provided");
	}

	public static Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string method = request.RequestContext.HttpMethod;
		string path = request.RequestContext.Path;

		if (method == "POST" && path == $"/{stageName}/reserve")
		{
			return HandleReserveAsync(request, context);
		}
		else if (method == "GET" && path == $"/{stageName}/seats")
		{
			return HandleSeatsAsync(request, context);
		}

		return Task.FromResult(new APIGatewayProxyResponse
		{
			StatusCode = 404,
			Body = "Route not found"
		});
	}

	private static async Task<APIGatewayProxyResponse> HandleReserveAsync(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string eventId = "1";
		Reservation reservation = JsonSerializer.Deserialize<Reservation>(request.Body, jsonSerializerOptions)!;

		RedisValue[] seatIds = reservation.Seats.Select(s => (RedisValue)$"{s.Row}_{s.Col}").ToArray();
		RedisValue[] currentHolders = await redisDb.HashGetAsync($"event:{eventId}:seats", seatIds);

		if (currentHolders.Any(x => x != reservation.UserId))
		{
			return new APIGatewayProxyResponse { StatusCode = 400, Body = "Already reserved seats" };
		}

		Table table = Table.LoadTable(dynamoDbClient, tableName);

		foreach (var seat in reservation.Seats)
		{
			var document = new Document
			{
				["pk"] = $"event#{eventId}",
				["sk"] = $"seat#{seat.Row}_{seat.Col}",
				["payload"] = new Document
				{
					["user_id"] = reservation.UserId,
				},
			};

			await table.PutItemAsync(document);
		}

		return new APIGatewayProxyResponse { StatusCode = 200 };
	}

	private static async Task<APIGatewayProxyResponse> HandleSeatsAsync(APIGatewayProxyRequest request, ILambdaContext context)
	{
		string eventId = "1";
		string userId = request.QueryStringParameters["userId"];
		Dictionary<RedisValue, RedisValue> seats = (await redisDb.HashGetAllAsync($"event:{eventId}:seats")).ToDictionary(x => x.Name, x => x.Value);

		List<List<object>> response = new(10);
		for (int row = 0; row < 10; row++)
		{
			response.Add(new List<object>(12));

			for (int col = 0; col < 12; col++)
			{
				bool isReserved = seats.TryGetValue($"{row}_{col}", out RedisValue holderUserId);
				response[row].Add(new
				{
					row,
					col,
					isReserved,
					isMine = isReserved && holderUserId == userId,
				});
			}
		}

		return new APIGatewayProxyResponse
		{
			StatusCode = 200,
			Body = JsonSerializer.Serialize(response),
			Headers = new Dictionary<string, string?>
			{
				{ "Content-Type", "application/json" },
			},
		};
	}
}
