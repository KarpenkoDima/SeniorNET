variable "environment" {
  type        = string
  description = "Environment name"
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "location" {
  type    = string
  default = "West Europe"
}

variable "app_service_sku" {
  type    = string
  default = "B1"
}

variable "acr_name" {
  type        = string
  description = "Azure Container Registry name"
}

variable "image_tag" {
  type    = string
  default = "latest"
}

variable "db_admin_login" {
  type      = string
  sensitive = true
}

variable "db_admin_password" {
  type      = string
  sensitive = true
}

variable "db_sku" {
  type    = string
  default = "B_Standard_B1ms"
}

variable "db_storage_mb" {
  type    = number
  default = 32768
}

variable "redis_capacity" {
  type    = number
  default = 0
}

variable "redis_sku" {
  type    = string
  default = "Basic"
}

variable "rabbitmq_host" {
  type    = string
  default = "localhost"
}

variable "rabbitmq_user" {
  type    = string
  default = "guest"
}

variable "rabbitmq_password" {
  type      = string
  sensitive = true
  default   = "guest"
}
