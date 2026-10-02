# Sample Infrastructure Specification (Terraform)
# For CloudSealed Predictive-ML-Core IaC Architecture Risk Audit

resource "aws_db_instance" "primary_postgres" {
  allocated_storage    = 20
  engine               = "postgres"
  engine_version       = "15.3"
  instance_class       = "db.t3.micro"
  db_name              = "main_production_db"
  username             = "postgres"
  password             = "supersecret"
  publicly_accessible  = false # Protected Database
}

resource "aws_alb" "public_api_gateway" {
  name               = "checkout-api-alb"
  internal           = false # Public Facing
  load_balancer_type = "application"
}

resource "aws_ecs_service" "checkout_api" {
  name            = "checkout-api"
  cluster         = "prod-cluster"
  task_definition = "checkout-api-task"
  desired_count   = 1 # Single Point of Failure (SPOF) - 1 Instance
}

resource "aws_s3_bucket" "unauthenticated_data_dump" {
  bucket = "company-public-data-dump"
  # Unauthenticated exposure
}
