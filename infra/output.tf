output "http_api_endpoint" {
  value = aws_apigatewayv2_api.api.api_endpoint
}

output "websocket_api_endpoint" {
  value = "${aws_apigatewayv2_api.websocket.api_endpoint}/${var.stage_name}"
}
