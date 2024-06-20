provider "aws" {
  region = var.region
}

provider "awscc" {
  region = var.region
}

provider "http" {
}

provider "random" {
}
