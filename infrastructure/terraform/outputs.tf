output "api_url" {
  value       = "https://${azurerm_linux_web_app.api.default_hostname}"
  description = "URL of the deployed API"
}

output "postgresql_fqdn" {
  value       = azurerm_postgresql_flexible_server.main.fqdn
  description = "PostgreSQL server FQDN"
}

output "redis_hostname" {
  value       = azurerm_redis_cache.main.hostname
  description = "Redis cache hostname"
}

output "app_insights_connection_string" {
  value     = azurerm_application_insights.main.connection_string
  sensitive = true
}
