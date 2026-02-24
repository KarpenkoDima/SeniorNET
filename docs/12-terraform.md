# Terraform & Infrastructure as Code — подготовка к Senior .NET собеседованию

## Содержание

1. [Что такое Infrastructure as Code (IaC)](#1-что-такое-infrastructure-as-code-iac)
2. [Terraform: основы и архитектура](#2-terraform-основы-и-архитектура)
3. [HCL: язык конфигурации](#3-hcl-язык-конфигурации)
4. [Providers и ресурсы](#4-providers-и-ресурсы)
5. [State: управление состоянием](#5-state-управление-состоянием)
6. [Модули: переиспользование кода](#6-модули-переиспользование-кода)
7. [Переменные и outputs](#7-переменные-и-outputs)
8. [Data Sources](#8-data-sources)
9. [Provisioners и альтернативы](#9-provisioners-и-альтернативы)
10. [Workspaces и environments](#10-workspaces-и-environments)
11. [Azure-инфраструктура для .NET приложений](#11-azure-инфраструктура-для-net-приложений)
12. [AWS-инфраструктура для .NET приложений](#12-aws-инфраструктура-для-net-приложений)
13. [Kubernetes-кластер через Terraform](#13-kubernetes-кластер-через-terraform)
14. [CI/CD интеграция](#14-cicd-интеграция)
15. [Best Practices и антипаттерны](#15-best-practices-и-антипаттерны)
16. [Вопросы на собеседовании с ответами](#16-вопросы-на-собеседовании-с-ответами)

---

## 1. Что такое Infrastructure as Code (IaC)

### Определение

Infrastructure as Code — подход к управлению инфраструктурой, при котором серверы, сети, базы данных и другие ресурсы описываются в конфигурационных файлах, а не создаются вручную через UI.

### Преимущества IaC

| Преимущество | Описание |
|---|---|
| **Воспроизводимость** | Одна конфигурация → идентичные окружения (dev, staging, prod) |
| **Версионирование** | Конфигурация в Git — история изменений, code review, rollback |
| **Автоматизация** | Нет ручных действий → нет человеческих ошибок |
| **Документация** | Код = актуальная документация инфраструктуры |
| **Масштабируемость** | Создать 100 серверов так же просто, как 1 |
| **Тестируемость** | Можно валидировать конфигурацию до применения |

### Декларативный vs Императивный подходы

| Подход | Описание | Инструменты |
|---|---|---|
| **Декларативный** | «Что» должно быть — система сама разберётся «как» | Terraform, CloudFormation, Pulumi |
| **Императивный** | «Как» это сделать — последовательность шагов | Ansible (частично), скрипты Bash/PowerShell |

### Terraform vs другие инструменты

| Инструмент | Язык | Multi-Cloud | State | Подход |
|---|---|---|---|---|
| **Terraform** | HCL | Да | Внешний state file | Декларативный |
| **Pulumi** | C#, TypeScript, Python | Да | Внешний state | Декларативный (на языках программирования) |
| **CloudFormation** | JSON/YAML | Только AWS | Managed by AWS | Декларативный |
| **Bicep** | Bicep DSL | Только Azure | Managed by Azure | Декларативный |
| **Ansible** | YAML | Да | Нет state | Императивный / Декларативный |

> **Для .NET-разработчика:** Если вам ближе C#, посмотрите на **Pulumi** — он позволяет описывать инфраструктуру на C# с полной типизацией. Но Terraform — отраслевой стандарт, и его знание обязательно.

---

## 2. Terraform: основы и архитектура

### Как работает Terraform

```
                    ┌──────────────────┐
   .tf файлы ──→   │  terraform plan   │ ──→ Execution Plan
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │  terraform apply  │ ──→ Реальные ресурсы
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │   State File      │ ──→ terraform.tfstate
                    └──────────────────┘
```

### Ключевые команды

```bash
# Инициализация — скачивание провайдеров и модулей
terraform init

# Планирование — показывает, что будет изменено
terraform plan

# Применение изменений
terraform apply

# Применение без интерактивного подтверждения (для CI/CD)
terraform apply -auto-approve

# Уничтожение всех ресурсов
terraform destroy

# Форматирование файлов
terraform fmt -recursive

# Валидация конфигурации
terraform validate

# Показать текущее состояние
terraform state list
terraform state show <resource>

# Импорт существующего ресурса в state
terraform import <resource_type>.<name> <resource_id>
```

### Жизненный цикл ресурса

```
terraform plan:
  + create    — новый ресурс
  ~ update    — изменение in-place
  -/+ replace — удаление и создание заново (force new)
  - destroy   — удаление
```

---

## 3. HCL: язык конфигурации

### Основные конструкции

```hcl
# Локальные значения
locals {
  environment = "production"
  common_tags = {
    Environment = local.environment
    Project     = "SeniorNET"
    ManagedBy   = "Terraform"
  }
}

# Ресурс
resource "azurerm_resource_group" "main" {
  name     = "rg-seniornet-${local.environment}"
  location = var.location
  tags     = local.common_tags
}

# Условные выражения
resource "azurerm_redis_cache" "cache" {
  count = var.enable_redis ? 1 : 0

  name                = "redis-seniornet"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  capacity            = 1
  family              = "C"
  sku_name            = "Standard"
}

# Циклы — for_each
resource "azurerm_app_service" "services" {
  for_each = toset(["orders-api", "payments-api", "notifications-api"])

  name                = each.key
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  app_service_plan_id = azurerm_app_service_plan.main.id
}

# Динамические блоки
resource "azurerm_network_security_group" "main" {
  name                = "nsg-main"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  dynamic "security_rule" {
    for_each = var.security_rules
    content {
      name                       = security_rule.value.name
      priority                   = security_rule.value.priority
      direction                  = security_rule.value.direction
      access                     = security_rule.value.access
      protocol                   = security_rule.value.protocol
      source_port_range          = security_rule.value.source_port_range
      destination_port_range     = security_rule.value.destination_port_range
      source_address_prefix      = security_rule.value.source_address_prefix
      destination_address_prefix = security_rule.value.destination_address_prefix
    }
  }
}
```

### Встроенные функции

```hcl
# Строки
name = lower("MyApp")                     # "myapp"
name = replace("hello-world", "-", "_")   # "hello_world"
name = format("app-%s-%s", var.name, var.env)

# Коллекции
ids  = [for s in var.services : s.id]
map  = { for s in var.services : s.name => s.id }
filtered = [for s in var.services : s if s.enabled]

# Файлы
config = file("${path.module}/config.json")
template = templatefile("${path.module}/user-data.sh", {
  db_host = azurerm_postgresql_server.main.fqdn
})

# Кодирование
encoded = base64encode("secret")
decoded = jsondecode(file("config.json"))

# Проверки
cidr = cidrsubnet("10.0.0.0/16", 8, 1)   # "10.0.1.0/24"
```

---

## 4. Providers и ресурсы

### Настройка провайдера

```hcl
# versions.tf — фиксируем версии
terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.30"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.24"
    }
  }
}

# Провайдер Azure
provider "azurerm" {
  features {}
  subscription_id = var.azure_subscription_id
}

# Провайдер AWS
provider "aws" {
  region  = var.aws_region
  profile = var.aws_profile
}

# Несколько экземпляров одного провайдера (alias)
provider "aws" {
  alias  = "us_west"
  region = "us-west-2"
}

provider "aws" {
  alias  = "eu_west"
  region = "eu-west-1"
}

resource "aws_s3_bucket" "backup" {
  provider = aws.eu_west
  bucket   = "backup-eu-west"
}
```

### Зависимости между ресурсами

```hcl
# Implicit dependency — Terraform определяет автоматически
resource "azurerm_resource_group" "main" {
  name     = "rg-app"
  location = "West Europe"
}

resource "azurerm_virtual_network" "main" {
  name                = "vnet-app"
  # Terraform знает, что сначала нужен resource group
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  address_space       = ["10.0.0.0/16"]
}

# Explicit dependency — когда Terraform не может определить сам
resource "azurerm_app_service" "api" {
  # ...
  depends_on = [azurerm_postgresql_database.main]
}
```

### Lifecycle

```hcl
resource "azurerm_app_service" "api" {
  # ...

  lifecycle {
    # Не удалять старый ресурс до создания нового
    create_before_destroy = true

    # Игнорировать изменения, сделанные вне Terraform
    ignore_changes = [tags, app_settings["WEBSITE_RUN_FROM_PACKAGE"]]

    # Запретить удаление (защита от terraform destroy)
    prevent_destroy = true
  }
}
```

---

## 5. State: управление состоянием

### Что такое State

State file (`terraform.tfstate`) — JSON-файл, который хранит маппинг между ресурсами в конфигурации и реальными ресурсами в облаке.

### Проблемы локального state

- Нельзя работать в команде (конфликты)
- Нет блокировки (два `terraform apply` одновременно = катастрофа)
- Может содержать секреты в открытом виде

### Remote State (обязательно для команд)

```hcl
# Azure Blob Storage backend
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "tfstateseniornet"
    container_name       = "tfstate"
    key                  = "production.terraform.tfstate"
  }
}

# AWS S3 + DynamoDB backend
terraform {
  backend "s3" {
    bucket         = "my-terraform-state"
    key            = "production/terraform.tfstate"
    region         = "eu-west-1"
    dynamodb_table = "terraform-lock"  # Для блокировки
    encrypt        = true
  }
}
```

### Команды для работы со state

```bash
# Показать все ресурсы в state
terraform state list

# Детали конкретного ресурса
terraform state show azurerm_resource_group.main

# Переименовать ресурс (без пересоздания)
terraform state mv azurerm_app_service.old azurerm_app_service.new

# Удалить ресурс из state (не удаляя реальный ресурс)
terraform state rm azurerm_app_service.legacy

# Импортировать существующий ресурс
terraform import azurerm_resource_group.main /subscriptions/.../resourceGroups/rg-main
```

### State Locking

Предотвращает параллельное выполнение `terraform apply`:
- **Azure**: автоматически через Blob Lease
- **AWS**: через DynamoDB таблицу
- **Terraform Cloud**: встроенная блокировка

---

## 6. Модули: переиспользование кода

### Структура модуля

```
modules/
├── app-service/
│   ├── main.tf          # Ресурсы
│   ├── variables.tf     # Входные переменные
│   ├── outputs.tf       # Выходные значения
│   └── README.md        # Документация
├── database/
│   ├── main.tf
│   ├── variables.tf
│   └── outputs.tf
└── networking/
    ├── main.tf
    ├── variables.tf
    └── outputs.tf
```

### Создание модуля

```hcl
# modules/app-service/variables.tf
variable "name" {
  type        = string
  description = "Name of the app service"
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "sku" {
  type = object({
    tier = string
    size = string
  })
  default = {
    tier = "Standard"
    size = "S1"
  }
}

variable "dotnet_version" {
  type    = string
  default = "v8.0"
}

variable "app_settings" {
  type    = map(string)
  default = {}
}

variable "connection_strings" {
  type = map(object({
    value = string
    type  = string
  }))
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}
```

```hcl
# modules/app-service/main.tf
resource "azurerm_service_plan" "this" {
  name                = "plan-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "${substr(var.sku.tier, 0, 1)}${var.sku.size}"
}

resource "azurerm_linux_web_app" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id

  site_config {
    application_stack {
      dotnet_version = var.dotnet_version
    }
    always_on        = true
    health_check_path = "/health"
  }

  app_settings = merge({
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "DOTNET_RUNNING_IN_CONTAINER" = "false"
  }, var.app_settings)

  dynamic "connection_string" {
    for_each = var.connection_strings
    content {
      name  = connection_string.key
      value = connection_string.value.value
      type  = connection_string.value.type
    }
  }

  tags = var.tags
}
```

```hcl
# modules/app-service/outputs.tf
output "id" {
  value = azurerm_linux_web_app.this.id
}

output "default_hostname" {
  value = azurerm_linux_web_app.this.default_hostname
}

output "outbound_ip_addresses" {
  value = azurerm_linux_web_app.this.outbound_ip_addresses
}
```

### Использование модуля

```hcl
# main.tf — корневая конфигурация
module "orders_api" {
  source = "./modules/app-service"

  name                = "app-orders-api-prod"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  dotnet_version = "v8.0"
  sku = {
    tier = "Standard"
    size = "S2"
  }

  app_settings = {
    "ConnectionStrings__Redis" = module.redis.connection_string
    "ServiceBus__ConnectionString" = module.servicebus.connection_string
  }

  connection_strings = {
    "DefaultConnection" = {
      value = module.database.connection_string
      type  = "SQLAzure"
    }
  }

  tags = local.common_tags
}

module "payments_api" {
  source = "./modules/app-service"

  name                = "app-payments-api-prod"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  dotnet_version      = "v8.0"
  tags                = local.common_tags
}
```

---

## 7. Переменные и outputs

### Типы переменных

```hcl
# Примитивные типы
variable "location" {
  type    = string
  default = "West Europe"
}

variable "instance_count" {
  type    = number
  default = 2
}

variable "enable_monitoring" {
  type    = bool
  default = true
}

# Коллекции
variable "allowed_ips" {
  type    = list(string)
  default = ["10.0.0.0/8", "172.16.0.0/12"]
}

variable "app_settings" {
  type    = map(string)
  default = {}
}

variable "services" {
  type = list(object({
    name     = string
    port     = number
    replicas = number
    enabled  = bool
  }))
}

# Валидация
variable "environment" {
  type = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "instance_count" {
  type = number
  validation {
    condition     = var.instance_count >= 1 && var.instance_count <= 10
    error_message = "Instance count must be between 1 and 10."
  }
}

# Sensitive — не показывается в логах
variable "db_password" {
  type      = string
  sensitive = true
}
```

### Приоритет значений переменных (от низшего к высшему)

1. `default` в `variable` блоке
2. `terraform.tfvars` / `*.auto.tfvars`
3. `-var-file` аргумент
4. `-var` аргумент
5. `TF_VAR_*` переменные окружения

```hcl
# terraform.tfvars
location       = "West Europe"
environment    = "production"
instance_count = 3
```

```bash
# Через переменную окружения
export TF_VAR_db_password="supersecret"
terraform apply

# Через аргумент
terraform apply -var="environment=staging"
```

### Outputs

```hcl
output "api_url" {
  value       = "https://${module.orders_api.default_hostname}"
  description = "URL of the Orders API"
}

output "db_connection_string" {
  value     = module.database.connection_string
  sensitive = true
}

# Использование output другого state (remote state)
data "terraform_remote_state" "network" {
  backend = "azurerm"
  config = {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "tfstate"
    container_name       = "tfstate"
    key                  = "network.terraform.tfstate"
  }
}

resource "azurerm_linux_web_app" "api" {
  # Используем output из другого state
  subnet_id = data.terraform_remote_state.network.outputs.app_subnet_id
}
```

---

## 8. Data Sources

Data sources позволяют запрашивать информацию о существующих ресурсах.

```hcl
# Получить информацию о существующем ресурсе
data "azurerm_client_config" "current" {}

data "azurerm_resource_group" "existing" {
  name = "rg-shared-services"
}

data "azurerm_key_vault" "main" {
  name                = "kv-seniornet-prod"
  resource_group_name = data.azurerm_resource_group.existing.name
}

# Прочитать секрет из Key Vault
data "azurerm_key_vault_secret" "db_password" {
  name         = "db-admin-password"
  key_vault_id = data.azurerm_key_vault.main.id
}

# Использование
resource "azurerm_postgresql_server" "main" {
  name                = "psql-seniornet"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  administrator_login          = "psqladmin"
  administrator_login_password = data.azurerm_key_vault_secret.db_password.value

  sku_name   = "GP_Gen5_2"
  version    = "11"
  storage_mb = 51200
}

# AWS: получить последний AMI
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }
}
```

---

## 9. Provisioners и альтернативы

### Provisioners (используйте как последнее средство)

```hcl
resource "aws_instance" "web" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = "t3.medium"

  # Выполнить на удалённой машине после создания
  provisioner "remote-exec" {
    inline = [
      "sudo apt-get update",
      "sudo apt-get install -y dotnet-sdk-8.0"
    ]

    connection {
      type        = "ssh"
      user        = "ubuntu"
      private_key = file("~/.ssh/id_rsa")
      host        = self.public_ip
    }
  }

  # Выполнить на локальной машине
  provisioner "local-exec" {
    command = "echo ${self.private_ip} >> inventory.txt"
  }
}
```

### Лучшие альтернативы

| Вместо provisioner | Используйте |
|---|---|
| Установка ПО | Packer (создание AMI/образа) |
| Конфигурация сервера | Ansible, cloud-init |
| Запуск скриптов | User Data (AWS), Custom Script Extension (Azure) |
| Приложения | Docker, Kubernetes |

```hcl
# Лучше: cloud-init через user_data
resource "aws_instance" "web" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = "t3.medium"

  user_data = templatefile("${path.module}/cloud-init.yaml", {
    dotnet_version = "8.0"
    app_name       = var.app_name
  })
}
```

---

## 10. Workspaces и environments

### Terraform Workspaces

```bash
# Создать и переключить workspace
terraform workspace new staging
terraform workspace new production

# Список
terraform workspace list

# Переключение
terraform workspace select staging
```

```hcl
# Использование workspace в конфигурации
locals {
  environment = terraform.workspace

  config = {
    dev = {
      instance_count = 1
      sku            = "B1"
      enable_redis   = false
    }
    staging = {
      instance_count = 2
      sku            = "S1"
      enable_redis   = true
    }
    production = {
      instance_count = 3
      sku            = "P1v2"
      enable_redis   = true
    }
  }

  current_config = local.config[local.environment]
}

resource "azurerm_service_plan" "main" {
  name     = "plan-app-${local.environment}"
  sku_name = local.current_config.sku
  # ...
}
```

### Альтернатива: отдельные директории (рекомендуется для production)

```
infrastructure/
├── modules/
│   ├── app-service/
│   ├── database/
│   └── networking/
├── environments/
│   ├── dev/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── terraform.tfvars
│   ├── staging/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── terraform.tfvars
│   └── production/
│       ├── main.tf
│       ├── variables.tf
│       └── terraform.tfvars
```

> **Совет:** Для production-систем отдельные директории предпочтительнее workspaces — это снижает риск случайного применения production-изменений, и каждый environment имеет свой state file.

---

## 11. Azure-инфраструктура для .NET приложений

### Полный пример: Web API + SQL + Redis + Service Bus

```hcl
# main.tf — полная инфраструктура для .NET микросервиса
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
    key                  = "app.terraform.tfstate"
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

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.project}-${local.environment}"
  location = local.location
  tags     = local.common_tags
}

# App Service Plan
resource "azurerm_service_plan" "main" {
  name                = "plan-${local.project}-${local.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.common_tags
}

# App Service
resource "azurerm_linux_web_app" "api" {
  name                = "app-${local.project}-api-${local.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
    always_on         = true
    health_check_path = "/health"
    ftps_state        = "Disabled"
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"           = local.environment == "production" ? "Production" : "Development"
    "ConnectionStrings__Redis"         = azurerm_redis_cache.main.primary_connection_string
    "ServiceBus__ConnectionString"     = azurerm_servicebus_namespace.main.default_primary_connection_string
    "ApplicationInsights__ConnectionString" = azurerm_application_insights.main.connection_string
  }

  connection_string {
    name  = "DefaultConnection"
    type  = "SQLAzure"
    value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.main.name};User ID=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=true;"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.project}-${local.environment}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  tags                         = local.common_tags
}

resource "azurerm_mssql_database" "main" {
  name      = "db-${local.project}"
  server_id = azurerm_mssql_server.main.id
  sku_name  = var.sql_sku
  tags      = local.common_tags
}

# Firewall: разрешить Azure-сервисам
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Redis Cache
resource "azurerm_redis_cache" "main" {
  name                = "redis-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  capacity            = var.redis_capacity
  family              = var.redis_family
  sku_name            = var.redis_sku
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"
  tags                = local.common_tags
}

# Service Bus
resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard"
  tags                = local.common_tags
}

resource "azurerm_servicebus_queue" "orders" {
  name         = "orders"
  namespace_id = azurerm_servicebus_namespace.main.id

  max_delivery_count  = 10
  lock_duration       = "PT1M"
  dead_lettering_on_message_expiration = true
}

# Application Insights
resource "azurerm_application_insights" "main" {
  name                = "ai-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"
  tags                = local.common_tags
}

# Outputs
output "api_url" {
  value = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}
```

---

## 12. AWS-инфраструктура для .NET приложений

### ECS Fargate для .NET API

```hcl
# VPC
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.0"

  name = "${local.project}-vpc"
  cidr = "10.0.0.0/16"

  azs             = ["eu-west-1a", "eu-west-1b"]
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

  enable_nat_gateway   = true
  single_nat_gateway   = var.environment != "production"
  enable_dns_hostnames = true

  tags = local.common_tags
}

# ECR Repository для Docker-образов
resource "aws_ecr_repository" "api" {
  name                 = "${local.project}-api"
  image_tag_mutability = "IMMUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

# ECS Cluster
resource "aws_ecs_cluster" "main" {
  name = "${local.project}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# Task Definition
resource "aws_ecs_task_definition" "api" {
  family                   = "${local.project}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.task_cpu
  memory                   = var.task_memory
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name  = "api"
      image = "${aws_ecr_repository.api.repository_url}:${var.image_tag}"

      portMappings = [{
        containerPort = 8080
        protocol      = "tcp"
      }]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" }
      ]

      secrets = [
        {
          name      = "ConnectionStrings__DefaultConnection"
          valueFrom = aws_ssm_parameter.db_connection.arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "api"
        }
      }

      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
    }
  ])
}

# ECS Service с ALB
resource "aws_ecs_service" "api" {
  name            = "${local.project}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets         = module.vpc.private_subnets
    security_groups = [aws_security_group.ecs.id]
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }
}

# Auto Scaling
resource "aws_appautoscaling_target" "api" {
  max_capacity       = 10
  min_capacity       = var.desired_count
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.api.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "cpu" {
  name               = "cpu-auto-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension
  service_namespace  = aws_appautoscaling_target.api.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value = 70.0
  }
}
```

---

## 13. Kubernetes-кластер через Terraform

### AKS (Azure Kubernetes Service)

```hcl
resource "azurerm_kubernetes_cluster" "main" {
  name                = "aks-${local.project}-${local.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "${local.project}-${local.environment}"
  kubernetes_version  = var.kubernetes_version

  default_node_pool {
    name                = "system"
    node_count          = var.system_node_count
    vm_size             = "Standard_D2s_v3"
    os_disk_size_gb     = 50
    vnet_subnet_id      = azurerm_subnet.aks.id
    enable_auto_scaling = true
    min_count           = 2
    max_count           = 5
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin    = "azure"
    load_balancer_sku = "standard"
    service_cidr      = "10.1.0.0/16"
    dns_service_ip    = "10.1.0.10"
  }

  oms_agent {
    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  }

  tags = local.common_tags
}

# Отдельный node pool для приложений
resource "azurerm_kubernetes_cluster_node_pool" "apps" {
  name                  = "apps"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.main.id
  vm_size               = "Standard_D4s_v3"
  enable_auto_scaling   = true
  min_count             = 1
  max_count             = 10
  vnet_subnet_id        = azurerm_subnet.aks.id

  node_labels = {
    "workload" = "application"
  }

  tags = local.common_tags
}

# Деплой в K8s через Terraform Kubernetes provider
provider "kubernetes" {
  host                   = azurerm_kubernetes_cluster.main.kube_config[0].host
  client_certificate     = base64decode(azurerm_kubernetes_cluster.main.kube_config[0].client_certificate)
  client_key             = base64decode(azurerm_kubernetes_cluster.main.kube_config[0].client_key)
  cluster_ca_certificate = base64decode(azurerm_kubernetes_cluster.main.kube_config[0].cluster_ca_certificate)
}

resource "kubernetes_namespace" "app" {
  metadata {
    name = local.project
    labels = {
      environment = local.environment
    }
  }
}
```

---

## 14. CI/CD интеграция

### GitHub Actions + Terraform

```yaml
# .github/workflows/terraform.yml
name: Terraform

on:
  push:
    branches: [main]
    paths: ['infrastructure/**']
  pull_request:
    branches: [main]
    paths: ['infrastructure/**']

env:
  TF_WORKING_DIR: infrastructure/environments/production
  ARM_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
  ARM_CLIENT_SECRET: ${{ secrets.AZURE_CLIENT_SECRET }}
  ARM_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
  ARM_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}

jobs:
  plan:
    name: Terraform Plan
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: 1.6.0

      - name: Terraform Init
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform init

      - name: Terraform Format Check
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform fmt -check -recursive

      - name: Terraform Validate
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform validate

      - name: Terraform Plan
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform plan -out=tfplan -no-color
        continue-on-error: true

      - name: Comment PR with Plan
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const output = `#### Terraform Plan 📖
            \`\`\`
            ${process.env.PLAN}
            \`\`\`
            `;
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: output
            })

  apply:
    name: Terraform Apply
    needs: plan
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4

      - uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: 1.6.0

      - name: Terraform Init
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform init

      - name: Terraform Apply
        working-directory: ${{ env.TF_WORKING_DIR }}
        run: terraform apply -auto-approve
```

### Terraform в Azure DevOps

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main]
  paths:
    include: [infrastructure/*]

pool:
  vmImage: 'ubuntu-latest'

stages:
  - stage: Plan
    jobs:
      - job: TerraformPlan
        steps:
          - task: TerraformInstaller@1
            inputs:
              terraformVersion: '1.6.0'

          - task: TerraformTaskV4@4
            displayName: 'Init'
            inputs:
              provider: 'azurerm'
              command: 'init'
              workingDirectory: 'infrastructure/environments/production'
              backendServiceArm: 'Azure-ServiceConnection'
              backendAzureRmResourceGroupName: 'rg-terraform-state'
              backendAzureRmStorageAccountName: 'tfstate'
              backendAzureRmContainerName: 'tfstate'
              backendAzureRmKey: 'production.tfstate'

          - task: TerraformTaskV4@4
            displayName: 'Plan'
            inputs:
              provider: 'azurerm'
              command: 'plan'
              workingDirectory: 'infrastructure/environments/production'
              environmentServiceNameAzureRM: 'Azure-ServiceConnection'

  - stage: Apply
    dependsOn: Plan
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - deployment: TerraformApply
        environment: 'production'
        strategy:
          runOnce:
            deploy:
              steps:
                - task: TerraformTaskV4@4
                  displayName: 'Apply'
                  inputs:
                    provider: 'azurerm'
                    command: 'apply'
                    workingDirectory: 'infrastructure/environments/production'
                    environmentServiceNameAzureRM: 'Azure-ServiceConnection'
```

---

## 15. Best Practices и антипаттерны

### Best Practices

| Практика | Описание |
|---|---|
| **Remote State** | Всегда используйте remote backend с блокировкой |
| **Фиксация версий** | Фиксируйте версии Terraform, провайдеров и модулей |
| **Модули** | Выделяйте повторяющуюся инфраструктуру в модули |
| **Naming convention** | Единообразные имена: `{type}-{project}-{environment}` |
| **Sensitive данные** | Секреты через Key Vault/SSM, не в .tf файлах |
| **State per environment** | Отдельный state для каждого окружения |
| **Plan before apply** | Всегда проверяйте plan перед apply |
| **Маленькие изменения** | Частые мелкие apply вместо редких больших |
| **Code review** | Terraform изменения через Pull Request |
| **Таги** | Все ресурсы с тегами для учёта и управления |

### Антипаттерны

| Антипаттерн | Проблема | Решение |
|---|---|---|
| Локальный state | Нет блокировки, конфликты | Remote backend |
| Hardcoded values | Невозможно переиспользовать | Variables и locals |
| Один огромный state | Медленный plan, blast radius | Разделение по слоям/сервисам |
| Ручные изменения | State drift | Политика: всё через Terraform |
| Секреты в коде | Утечка данных | Key Vault, SSM, `sensitive = true` |
| `terraform apply` без plan | Неожиданные изменения | Обязательный review plan |
| Provisioners для всего | Хрупкость, нет идемпотентности | Packer, cloud-init, Ansible |
| Нет `.gitignore` | State/секреты в Git | Игнорировать `.tfstate`, `.tfvars` |

### .gitignore для Terraform

```gitignore
# Local .terraform directories
**/.terraform/*

# .tfstate files
*.tfstate
*.tfstate.*

# Crash log files
crash.log
crash.*.log

# Sensitive variable files
*.tfvars
!example.tfvars

# Override files
override.tf
override.tf.json
*_override.tf
*_override.tf.json

# CLI configuration files
.terraformrc
terraform.rc

# Lock file (commit this!)
# !.terraform.lock.hcl
```

---

## 16. Вопросы на собеседовании с ответами

### Q1: Что такое Terraform State и зачем он нужен?

**Ответ:**
State — это JSON-файл, который хранит маппинг между конфигурацией (`.tf` файлы) и реальными ресурсами в облаке. Он нужен для:
- **Определения изменений**: Terraform сравнивает конфигурацию со state, чтобы понять, что нужно создать/обновить/удалить
- **Метаданные**: хранит зависимости между ресурсами
- **Производительность**: не нужно опрашивать облачное API для каждого ресурса
- **Блокировка**: предотвращает параллельные изменения

В команде обязательно использовать remote backend (S3, Azure Blob) с блокировкой.

### Q2: Разница между `count` и `for_each`?

**Ответ:**
- **`count`** — создаёт N одинаковых ресурсов. Ресурсы идентифицируются индексом (`[0]`, `[1]`). Проблема: при удалении элемента из середины, все последующие пересоздаются.
- **`for_each`** — создаёт ресурсы по ключам map/set. Ресурсы идентифицируются ключом (`["orders-api"]`). Удаление одного элемента не влияет на остальные.

**Рекомендация:** Используйте `for_each` для ресурсов, которые могут добавляться/удаляться. `count` — только для простого on/off (`count = var.enabled ? 1 : 0`).

### Q3: Как решить проблему state drift?

**Ответ:**
State drift — когда реальное состояние ресурсов расходится со state (кто-то изменил ресурс вручную).

Решения:
1. **`terraform plan`** — покажет расхождения
2. **`terraform refresh`** — обновит state из реального состояния (осторожно!)
3. **`terraform import`** — импортирует ресурс, созданный вне Terraform
4. **Политика**: все изменения только через Terraform + CI/CD
5. **Drift detection**: автоматический `terraform plan` по расписанию с оповещениями

### Q4: Как организовать Terraform для нескольких окружений?

**Ответ:**
Два основных подхода:
1. **Workspaces** — один набор `.tf` файлов, разные state'ы. Подходит для небольших проектов.
2. **Отдельные директории** — каждое окружение в своей папке, общие модули. Рекомендуется для production — изоляция state, отдельные permissions, меньше риск.

Можно комбинировать: модули переиспользуются, а переменные (`tfvars`) различаются для каждого окружения.

### Q5: Terraform vs Pulumi — когда что выбрать?

**Ответ:**
- **Terraform**: отраслевой стандарт, огромная экосистема модулей, HCL — простой DSL. Выбирайте, когда команда мультиязычная или нужна максимальная совместимость.
- **Pulumi**: используйте C#, TypeScript и другие языки. Полная сила IDE (IntelliSense, рефакторинг). Выбирайте, когда команда — .NET-разработчики и инфраструктура тесно связана с кодом приложения.

### Q6: Как безопасно удалить ресурс из Terraform без его физического удаления?

**Ответ:**
1. Добавить блок `removed` (Terraform 1.7+):
```hcl
removed {
  from = azurerm_redis_cache.old
  lifecycle {
    destroy = false
  }
}
```
2. Или вручную: `terraform state rm azurerm_redis_cache.old` — удаляет из state, но не трогает реальный ресурс.

> **Совет на собеседовании:** Покажите, что для вас Terraform — это не только `init/plan/apply`. Расскажите о модулях, remote state, CI/CD интеграции и стратегии управления окружениями. Это отличает Senior от Middle.
