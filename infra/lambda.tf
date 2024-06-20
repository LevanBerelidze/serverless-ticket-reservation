resource "aws_lambda_function" "api" {
  function_name = "${var.default_resource_name}-api"
  role          = aws_iam_role.lambda_role.arn

  # Code will be uploaded later
  runtime     = "dotnet8"
  handler     = "TicketReservation.Api::TicketReservation.Api.Function::FunctionHandler"
  memory_size = 512
  timeout     = 30
  filename    = "dummy-lambda-handler.zip"

  vpc_config {
    subnet_ids         = [aws_subnet.private1.id, aws_subnet.private2.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      ELASTICACHE_ENDPOINT_ADDRESS = awscc_elasticache_serverless_cache.main.endpoint.address
      ELASTICACHE_ENDPOINT_PORT    = awscc_elasticache_serverless_cache.main.endpoint.port
      DYNAMODB_TABLE_NAME          = aws_dynamodb_table.main.name
      STAGE_NAME                   = var.stage_name
    }
  }
}

resource "aws_lambda_function" "room" {
  function_name = "${var.default_resource_name}-room"
  role          = aws_iam_role.lambda_role.arn

  # Code will be uploaded later
  runtime     = "dotnet8"
  handler     = "TicketReservation.Room::TicketReservation.Room.Function::FunctionHandler"
  memory_size = 512
  timeout     = 30
  filename    = "dummy-lambda-handler.zip"

  vpc_config {
    subnet_ids         = [aws_subnet.private1.id, aws_subnet.private2.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      ELASTICACHE_ENDPOINT_ADDRESS   = awscc_elasticache_serverless_cache.main.endpoint.address
      ELASTICACHE_ENDPOINT_PORT      = awscc_elasticache_serverless_cache.main.endpoint.port
      WEBSOCKET_GATEWAY_API_ENDPOINT = aws_apigatewayv2_api.websocket.api_endpoint
      STAGE_NAME                     = var.stage_name
    }
  }
}

resource "aws_security_group" "lambda" {
  name   = "${var.default_resource_name}-lambda"
  vpc_id = aws_vpc.main.id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.default_resource_name}-lambda"
  }
}
