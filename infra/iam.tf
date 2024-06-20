resource "aws_iam_role" "lambda_role" {
  name = "LambdaExecutionRole"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
        Effect = "Allow"
        Sid    = ""
      },
    ]
  })
}

resource "aws_iam_role_policy_attachment" "aws_lambda_execute" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = "arn:aws:iam::aws:policy/AWSLambdaExecute"
}

resource "aws_iam_policy" "allow_vpc_access" {
  name = "AllowVPCAccess"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ec2:DescribeNetworkInterfaces",
          "ec2:CreateNetworkInterface",
          "ec2:DeleteNetworkInterface",
          "ec2:DescribeInstances",
          "ec2:AttachNetworkInterface"
        ]
        Resource = "*"
      },
    ]
  })
}

resource "aws_iam_role_policy_attachment" "custom_vpc_access" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.allow_vpc_access.arn
}

resource "aws_iam_policy" "allow_dynamodb_access" {
  name = "AllowDynamoDBAccess"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:UpdateItem",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:DeleteItem",
          "dynamodb:DescribeTable"
        ]
        Resource = "arn:aws:dynamodb:us-east-1:${data.aws_caller_identity.current.account_id}:table/${aws_dynamodb_table.main.id}"
      },
    ]
  })
}

resource "aws_iam_role_policy_attachment" "custom_dynamodb_access" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.allow_dynamodb_access.arn
}

resource "aws_iam_policy" "allow_access_to_websocket_gateway" {
  name = "AllowAccessToWebSocketGateway"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = "execute-api:Invoke"
        Resource = "arn:aws:execute-api:us-east-1:${data.aws_caller_identity.current.account_id}:${aws_apigatewayv2_api.websocket.id}/${var.stage_name}/POST/@connections/*"
      },
      {
        Effect   = "Allow"
        Action   = "execute-api:ManageConnections"
        Resource = "arn:aws:execute-api:us-east-1:${data.aws_caller_identity.current.account_id}:${aws_apigatewayv2_api.websocket.id}/${var.stage_name}/POST/@connections/*"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "custom_access_to_websocket_gateway" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.allow_access_to_websocket_gateway.arn
}
