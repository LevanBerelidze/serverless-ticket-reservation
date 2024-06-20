resource "awscc_elasticache_serverless_cache" "main" {
  engine                = "redis"
  serverless_cache_name = var.default_resource_name

  subnet_ids         = [aws_subnet.private1.id, aws_subnet.private2.id]
  security_group_ids = [aws_security_group.elasticache.id]

  cache_usage_limits = {
    data_storage = {
      maximum = 1
      unit    = "GB"
    }
    ecpu_per_second = {
      maximum = 1000
    }
  }
}

resource "aws_security_group" "elasticache" {
  name   = "${var.default_resource_name}-redis"
  vpc_id = aws_vpc.main.id

  ingress {
    from_port   = 6379
    to_port     = 6379
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.default_resource_name}-redis"
  }
}
