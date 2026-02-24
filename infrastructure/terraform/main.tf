terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "tfstateseniornet"
    container_name       = "tfstate"
    key                  = "seniornet.terraform.tfstate"
  }
}

provider "azurerm" {
  features {}
}

locals {
  project     = "seniornet"
  environment = var.environment
  location    = var.location
  common_tags = {
    Project     = local.project
    Environment = local.environment
    ManagedBy   = "Terraform"
  }
}

# --- Resource Group ---
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.project}-${local.environment}"
  location = local.location
  tags     = local.common_tags
}

# --- App Service Plan ---
resource "azurerm_service_plan" "main" {
  name                = "plan-${local.project}-${local.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.common_tags
}

# --- Web App (API) ---
resource "azurerm_linux_web_app" "api" {
  name                = "app-${local.project}-api-${local.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  site_config {
    application_stack {
      docker_image_name = "${var.acr_name}.azurecr.io/${local.project}-api:${var.image_tag}"
    }
    always_on         = true
    health_check_path = "/health"
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"       = local.environment == "production" ? "Production" : "Development"
    "ConnectionStrings__Redis"     = azurerm_redis_cache.main.primary_connection_string
    "ConnectionStrings__RabbitMQ"  = "amqp://${var.rabbitmq_user}:${var.rabbitmq_password}@${var.rabbitmq_host}:5672"
  }

  connection_string {
    name  = "DefaultConnection"
    type  = "PostgreSQL"
    value = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.main.name};Username=${var.db_admin_login};Password=${var.db_admin_password};SslMode=Require"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# --- PostgreSQL ---
resource "azurerm_postgresql_flexible_server" "main" {
  name                   = "psql-${local.project}-${local.environment}"
  resource_group_name    = azurerm_resource_group.main.name
  location               = azurerm_resource_group.main.location
  version                = "16"
  administrator_login    = var.db_admin_login
  administrator_password = var.db_admin_password
  sku_name               = var.db_sku
  storage_mb             = var.db_storage_mb
  zone                   = "1"
  tags                   = local.common_tags
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = local.project
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# --- Redis ---
resource "azurerm_redis_cache" "main" {
  name                = "redis-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  capacity            = var.redis_capacity
  family              = "C"
  sku_name            = var.redis_sku
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"
  tags                = local.common_tags
}

# --- Application Insights ---
resource "azurerm_application_insights" "main" {
  name                = "ai-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"
  tags                = local.common_tags
}
