variable "region" {
  default = "us-east-1"
}

variable "vpc_cidr" {
  default = "10.0.0.0/16"
}

variable "public_subnet_cidr" {
  default = "10.0.1.0/24"
}

variable "public_subnet_az" {
  default = "us-east-1a"
}

variable "private_subnet1_cidr" {
  default = "10.0.2.0/24"
}

variable "private_subnet1_az" {
  default = "us-east-1b"
}

variable "private_subnet2_cidr" {
  default = "10.0.3.0/24"
}

variable "private_subnet2_az" {
  default = "us-east-1c"
}

variable "default_resource_name" {
  default = "ticket-reservation"
}

variable "stage_name" {
  default = "test"
}

data "aws_caller_identity" "current" {}
