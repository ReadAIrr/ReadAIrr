# ReadAIrr Development Makefile
# 
# This Makefile provides convenient targets for local development,
# including code indexing, building, testing, and Docker operations.

# Variables
PROJECT_NAME := ReadAIrr
SRC_DIR := src
FRONTEND_DIR := frontend
TOOLS_DIR := tools
INDEX_DIR := $(TOOLS_DIR)/index
DOCKER_COMPOSE_DEV := docker-compose.dev.yml

# .NET Configuration
DOTNET_CONFIG := Release
DOTNET_VERBOSITY := minimal

# Colors for terminal output
COLOR_RESET := \033[0m
COLOR_BOLD := \033[1m  
COLOR_GREEN := \033[32m
COLOR_BLUE := \033[34m
COLOR_YELLOW := \033[33m

# Help target (default)
.PHONY: help
help: ## Show this help message
	@echo "$(COLOR_BOLD)$(PROJECT_NAME) Development Makefile$(COLOR_RESET)"
	@echo ""
	@echo "$(COLOR_BOLD)Available targets:$(COLOR_RESET)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(COLOR_BLUE)%-20s$(COLOR_RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(COLOR_BOLD)Examples:$(COLOR_RESET)"
	@echo "  make index          # Generate all code indexes"
	@echo "  make build          # Build backend and frontend"
	@echo "  make test           # Run all tests"
	@echo "  make docker-up      # Start development environment"

# =============================================================================
# INDEX GENERATION TARGETS
# =============================================================================

.PHONY: index
index: index-backend index-frontend index-database index-api ## Generate all code indexes

.PHONY: index-backend
index-backend: ## Generate backend (C#) code index
	@echo "$(COLOR_GREEN)Generating backend code index...$(COLOR_RESET)"
	@mkdir -p $(INDEX_DIR)/backend
	@if command -v ctags >/dev/null 2>&1; then \
		ctags -R \
			--languages=C# \
			--exclude=bin \
			--exclude=obj \
			--exclude=packages \
			--exclude=TestResults \
			--tag-relative=yes \
			--fields=+iaS \
			--extra=+q \
			-f $(INDEX_DIR)/backend/tags.backend \
			$(SRC_DIR)/; \
		echo "$(COLOR_GREEN)✓ Backend CTags index generated$(COLOR_RESET)"; \
	else \
		echo "$(COLOR_YELLOW)⚠ ctags not found. Install universal-ctags for better indexing.$(COLOR_RESET)"; \
	fi
	@# Generate human-readable symbol summary
	@echo "# ReadAIrr Backend Symbol Index" > $(INDEX_DIR)/backend/symbols.md
	@echo "Generated on: $$(date)" >> $(INDEX_DIR)/backend/symbols.md
	@echo "" >> $(INDEX_DIR)/backend/symbols.md
	@if [ -f "$(INDEX_DIR)/backend/tags.backend" ]; then \
		echo "## Classes (Top 50)" >> $(INDEX_DIR)/backend/symbols.md; \
		grep -E "^.*\tc\t" $(INDEX_DIR)/backend/tags.backend | head -50 | \
			awk -F'\t' '{printf "- `%s` in %s\n", $$1, $$2}' >> $(INDEX_DIR)/backend/symbols.md; \
		echo "" >> $(INDEX_DIR)/backend/symbols.md; \
		echo "## Methods (Top 50)" >> $(INDEX_DIR)/backend/symbols.md; \
		grep -E "^.*\tm\t" $(INDEX_DIR)/backend/tags.backend | head -50 | \
			awk -F'\t' '{printf "- `%s` in %s\n", $$1, $$2}' >> $(INDEX_DIR)/backend/symbols.md; \
		echo "$(COLOR_GREEN)✓ Backend symbol summary generated$(COLOR_RESET)"; \
	fi

.PHONY: index-frontend  
index-frontend: ## Generate frontend (TypeScript/JavaScript) code index
	@echo "$(COLOR_GREEN)Generating frontend code index...$(COLOR_RESET)"
	@mkdir -p $(INDEX_DIR)/frontend
	@# Generate TypeScript build info
	@if [ -d "$(FRONTEND_DIR)" ]; then \
		cd $(FRONTEND_DIR) && \
		if [ -f package.json ] && command -v yarn >/dev/null 2>&1; then \
			yarn install --silent --frozen-lockfile || echo "$(COLOR_YELLOW)⚠ Yarn install failed$(COLOR_RESET)"; \
			yarn tsc --build --verbose 2>/dev/null || true; \
			find . -name "*.tsbuildinfo" -exec cp {} ../$(INDEX_DIR)/frontend/ \; 2>/dev/null || true; \
		fi; \
	fi
	@# Generate frontend CTags
	@if command -v ctags >/dev/null 2>&1 && [ -d "$(FRONTEND_DIR)/src" ]; then \
		ctags -R \
			--languages=JavaScript,TypeScript \
			--exclude=node_modules \
			--exclude=build \
			--exclude=dist \
			--exclude=.cache \
			--tag-relative=yes \
			--fields=+iaS \
			--extra=+q \
			-f $(INDEX_DIR)/frontend/tags.frontend \
			$(FRONTEND_DIR)/src/; \
		echo "$(COLOR_GREEN)✓ Frontend CTags index generated$(COLOR_RESET)"; \
	fi
	@# Generate ESLint cache and quality report
	@if [ -d "$(FRONTEND_DIR)" ] && [ -f "$(FRONTEND_DIR)/package.json" ]; then \
		cd $(FRONTEND_DIR) && \
		yarn lint --cache --cache-location ../$(INDEX_DIR)/frontend/.eslintcache 2>/dev/null || true; \
		echo "# Frontend Code Quality Report" > ../$(INDEX_DIR)/frontend/quality.md; \
		echo "Generated on: $$(date)" >> ../$(INDEX_DIR)/frontend/quality.md; \
		echo "" >> ../$(INDEX_DIR)/frontend/quality.md; \
		echo "ESLint cache updated for faster subsequent runs." >> ../$(INDEX_DIR)/frontend/quality.md; \
		echo "$(COLOR_GREEN)✓ Frontend quality report generated$(COLOR_RESET)"; \
	fi

.PHONY: index-database
index-database: ## Generate database schema index
	@echo "$(COLOR_GREEN)Generating database schema index...$(COLOR_RESET)"
	@mkdir -p $(INDEX_DIR)/database
	@echo "# Database Schema Index" > $(INDEX_DIR)/database/index.md
	@echo "Generated on: $$(date)" >> $(INDEX_DIR)/database/index.md
	@echo "" >> $(INDEX_DIR)/database/index.md
	@if [ -d "$(SRC_DIR)/NzbDrone.Core/Datastore" ]; then \
		echo "## Entity Statistics" >> $(INDEX_DIR)/database/index.md; \
		echo "- Models: $$(find $(SRC_DIR) -name '*.cs' -path '*/Datastore/*' -not -path '*/Migrations/*' | wc -l)" >> $(INDEX_DIR)/database/index.md; \
		echo "- Migrations: $$(find $(SRC_DIR) -name '*.cs' -path '*/Migrations/*' | wc -l)" >> $(INDEX_DIR)/database/index.md; \
		echo "" >> $(INDEX_DIR)/database/index.md; \
		echo "## Core Entities" >> $(INDEX_DIR)/database/index.md; \
		find $(SRC_DIR) -name '*.cs' -path '*/Datastore/*' -not -path '*/Migrations/*' | \
			head -30 | \
			xargs basename -s .cs | \
			sort | \
			sed 's/^/- /' >> $(INDEX_DIR)/database/index.md; \
		echo "$(COLOR_GREEN)✓ Database schema index generated$(COLOR_RESET)"; \
	else \
		echo "$(COLOR_YELLOW)⚠ Database datastore directory not found$(COLOR_RESET)"; \
	fi

.PHONY: index-api
index-api: ## Generate API endpoints index
	@echo "$(COLOR_GREEN)Generating API endpoints index...$(COLOR_RESET)"
	@mkdir -p $(INDEX_DIR)/api
	@echo "# API Endpoints Index" > $(INDEX_DIR)/api/endpoints.md
	@echo "Generated on: $$(date)" >> $(INDEX_DIR)/api/endpoints.md
	@echo "" >> $(INDEX_DIR)/api/endpoints.md
	@echo "## Controllers" >> $(INDEX_DIR)/api/endpoints.md
	@find $(SRC_DIR) -name '*Controller.cs' | \
		head -50 | \
		xargs basename -s .cs | \
		sort | \
		sed 's/Controller$$//' | \
		sed 's/^/- /' >> $(INDEX_DIR)/api/endpoints.md
	@# Copy existing API documentation if available
	@if [ -f "docs/api/openapi.json" ]; then \
		cp docs/api/openapi.json $(INDEX_DIR)/api/; \
		echo "$(COLOR_GREEN)✓ OpenAPI specification copied$(COLOR_RESET)"; \
	fi
	@echo "$(COLOR_GREEN)✓ API endpoints index generated$(COLOR_RESET)"

.PHONY: index-summary
index-summary: index ## Generate index summary and usage guide
	@echo "$(COLOR_GREEN)Generating index summary...$(COLOR_RESET)"
	@cat > $(INDEX_DIR)/README.md << 'EOF'
# ReadAIrr Project Index

This directory contains generated indexes for the ReadAIrr project to improve development productivity.

## Contents

- `backend/` - C# code indexes and symbols
- `frontend/` - TypeScript/JavaScript code indexes  
- `database/` - Database schema information
- `api/` - API endpoints and documentation

## Usage

### Visual Studio Code

1. Install the "C# Extensions" extension
2. Install the "TypeScript Importer" extension
3. The workspace will automatically use these indexes

### Vim/Neovim

Add to your `.vimrc`:
```vim
set tags+=./tools/index/backend/tags.backend
set tags+=./tools/index/frontend/tags.frontend
```

### Emacs

Add to your configuration:
```elisp
(setq tags-table-list
      '("./tools/index/backend/tags.backend"
        "./tools/index/frontend/tags.frontend"))
```

### Command Line

```bash
# Find all references to a symbol
grep -n "YourSymbol" tools/index/backend/tags.backend

# Search for functions
grep -E "^.*\tf\t" tools/index/frontend/tags.frontend
```

## Regeneration

Run `make index` to regenerate all indexes locally.
EOF
	@echo "$(COLOR_GREEN)✓ Index summary generated$(COLOR_RESET)"

.PHONY: clean-index
clean-index: ## Clean all generated indexes
	@echo "$(COLOR_GREEN)Cleaning generated indexes...$(COLOR_RESET)"
	@rm -rf $(INDEX_DIR)
	@echo "$(COLOR_GREEN)✓ Indexes cleaned$(COLOR_RESET)"

# =============================================================================
# BUILD TARGETS
# =============================================================================

.PHONY: build
build: build-backend build-frontend ## Build both backend and frontend

.PHONY: build-backend
build-backend: ## Build .NET backend
	@echo "$(COLOR_GREEN)Building .NET backend...$(COLOR_RESET)"
	@dotnet restore $(SRC_DIR)/Readarr.sln --verbosity $(DOTNET_VERBOSITY)
	@dotnet build $(SRC_DIR)/Readarr.sln \
		--configuration $(DOTNET_CONFIG) \
		--no-restore \
		--verbosity $(DOTNET_VERBOSITY) \
		--property TreatWarningsAsErrors=false
	@echo "$(COLOR_GREEN)✓ Backend build completed$(COLOR_RESET)"

.PHONY: build-frontend
build-frontend: ## Build React frontend
	@echo "$(COLOR_GREEN)Building React frontend...$(COLOR_RESET)"
	@if [ -d "$(FRONTEND_DIR)" ] && [ -f "$(FRONTEND_DIR)/package.json" ]; then \
		cd $(FRONTEND_DIR) && \
		if command -v yarn >/dev/null 2>&1; then \
			yarn install --frozen-lockfile && \
			yarn build; \
		elif command -v npm >/dev/null 2>&1; then \
			npm ci && \
			npm run build; \
		else \
			echo "$(COLOR_YELLOW)⚠ Neither yarn nor npm found$(COLOR_RESET)"; \
			exit 1; \
		fi; \
	fi
	@echo "$(COLOR_GREEN)✓ Frontend build completed$(COLOR_RESET)"

.PHONY: clean
clean: ## Clean build artifacts
	@echo "$(COLOR_GREEN)Cleaning build artifacts...$(COLOR_RESET)"
	@dotnet clean $(SRC_DIR)/Readarr.sln --verbosity $(DOTNET_VERBOSITY) || true
	@rm -rf $(SRC_DIR)/*/bin $(SRC_DIR)/*/obj
	@if [ -d "$(FRONTEND_DIR)/build" ]; then rm -rf $(FRONTEND_DIR)/build; fi
	@if [ -d "$(FRONTEND_DIR)/dist" ]; then rm -rf $(FRONTEND_DIR)/dist; fi
	@echo "$(COLOR_GREEN)✓ Build artifacts cleaned$(COLOR_RESET)"

# =============================================================================
# TEST TARGETS  
# =============================================================================

.PHONY: test
test: test-backend test-frontend ## Run all tests

.PHONY: test-backend
test-backend: ## Run .NET backend tests
	@echo "$(COLOR_GREEN)Running backend tests...$(COLOR_RESET)"
	@dotnet test $(SRC_DIR)/Readarr.sln \
		--configuration $(DOTNET_CONFIG) \
		--no-build \
		--verbosity $(DOTNET_VERBOSITY) \
		--logger trx \
		--property TreatWarningsAsErrors=false
	@echo "$(COLOR_GREEN)✓ Backend tests completed$(COLOR_RESET)"

.PHONY: test-frontend
test-frontend: ## Run frontend tests
	@echo "$(COLOR_GREEN)Running frontend tests...$(COLOR_RESET)"
	@if [ -d "$(FRONTEND_DIR)" ] && [ -f "$(FRONTEND_DIR)/package.json" ]; then \
		cd $(FRONTEND_DIR) && \
		if command -v yarn >/dev/null 2>&1; then \
			yarn test --watchAll=false; \
		elif command -v npm >/dev/null 2>&1; then \
			npm test -- --watchAll=false; \
		fi; \
	fi
	@echo "$(COLOR_GREEN)✓ Frontend tests completed$(COLOR_RESET)"

# =============================================================================
# LINTING AND CODE QUALITY
# =============================================================================

.PHONY: lint
lint: lint-backend lint-frontend ## Run all linters

.PHONY: lint-backend
lint-backend: ## Lint .NET backend code
	@echo "$(COLOR_GREEN)Linting backend code...$(COLOR_RESET)"
	@dotnet format $(SRC_DIR)/Readarr.sln --verify-no-changes --verbosity $(DOTNET_VERBOSITY) || \
		echo "$(COLOR_YELLOW)⚠ Backend formatting issues found. Run 'make format-backend' to fix.$(COLOR_RESET)"

.PHONY: lint-frontend
lint-frontend: ## Lint frontend code
	@echo "$(COLOR_GREEN)Linting frontend code...$(COLOR_RESET)"
	@if [ -d "$(FRONTEND_DIR)" ] && [ -f "$(FRONTEND_DIR)/package.json" ]; then \
		cd $(FRONTEND_DIR) && \
		yarn lint && \
		yarn stylelint "src/**/*.css"; \
	fi

.PHONY: format
format: format-backend format-frontend ## Format all code

.PHONY: format-backend
format-backend: ## Format .NET backend code
	@echo "$(COLOR_GREEN)Formatting backend code...$(COLOR_RESET)"
	@dotnet format $(SRC_DIR)/Readarr.sln --verbosity $(DOTNET_VERBOSITY)

.PHONY: format-frontend
format-frontend: ## Format frontend code
	@echo "$(COLOR_GREEN)Formatting frontend code...$(COLOR_RESET)"
	@if [ -d "$(FRONTEND_DIR)" ]; then \
		cd $(FRONTEND_DIR) && \
		yarn prettier --write "src/**/*.{ts,tsx,js,jsx,css,json}"; \
	fi

# =============================================================================
# DOCKER TARGETS
# =============================================================================

.PHONY: docker-up
docker-up: ## Start development Docker environment
	@echo "$(COLOR_GREEN)Starting Docker development environment...$(COLOR_RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_DEV) up -d
	@echo "$(COLOR_GREEN)✓ Docker environment started$(COLOR_RESET)"
	@echo "$(COLOR_BLUE)ReadAIrr available at: http://localhost:8246$(COLOR_RESET)"

.PHONY: docker-down
docker-down: ## Stop development Docker environment
	@echo "$(COLOR_GREEN)Stopping Docker development environment...$(COLOR_RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_DEV) down
	@echo "$(COLOR_GREEN)✓ Docker environment stopped$(COLOR_RESET)"

.PHONY: docker-logs
docker-logs: ## View Docker container logs
	@docker-compose -f $(DOCKER_COMPOSE_DEV) logs -f

.PHONY: docker-build
docker-build: ## Build Docker images
	@echo "$(COLOR_GREEN)Building Docker images...$(COLOR_RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_DEV) build
	@echo "$(COLOR_GREEN)✓ Docker images built$(COLOR_RESET)"

.PHONY: docker-clean
docker-clean: ## Clean Docker containers and images
	@echo "$(COLOR_GREEN)Cleaning Docker resources...$(COLOR_RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_DEV) down -v --remove-orphans
	@docker system prune -f
	@echo "$(COLOR_GREEN)✓ Docker resources cleaned$(COLOR_RESET)"

# =============================================================================
# DEVELOPMENT HELPERS
# =============================================================================

.PHONY: setup
setup: ## Setup development environment
	@echo "$(COLOR_GREEN)Setting up development environment...$(COLOR_RESET)"
	@# Check prerequisites
	@command -v dotnet >/dev/null 2>&1 || { echo "$(COLOR_YELLOW)⚠ .NET SDK not found. Please install .NET 8 SDK.$(COLOR_RESET)"; exit 1; }
	@command -v node >/dev/null 2>&1 || { echo "$(COLOR_YELLOW)⚠ Node.js not found. Please install Node.js 18+.$(COLOR_RESET)"; exit 1; }
	@# Install dependencies
	@dotnet restore $(SRC_DIR)/Readarr.sln
	@if [ -d "$(FRONTEND_DIR)" ]; then \
		cd $(FRONTEND_DIR) && \
		if command -v yarn >/dev/null 2>&1; then \
			yarn install; \
		elif command -v npm >/dev/null 2>&1; then \
			npm install; \
		fi; \
	fi
	@# Generate initial indexes
	@$(MAKE) index-summary
	@echo "$(COLOR_GREEN)✓ Development environment setup completed$(COLOR_RESET)"

.PHONY: dev
dev: ## Start development with file watching
	@echo "$(COLOR_GREEN)Starting development mode...$(COLOR_RESET)"
	@# This could be enhanced to start file watchers
	@$(MAKE) docker-up

.PHONY: status
status: ## Show project status
	@echo "$(COLOR_BOLD)$(PROJECT_NAME) Project Status$(COLOR_RESET)"
	@echo ""
	@echo "$(COLOR_BOLD)Dependencies:$(COLOR_RESET)"
	@command -v dotnet >/dev/null 2>&1 && echo "  ✓ .NET SDK: $$(dotnet --version)" || echo "  ✗ .NET SDK: Not installed"
	@command -v node >/dev/null 2>&1 && echo "  ✓ Node.js: $$(node --version)" || echo "  ✗ Node.js: Not installed"
	@command -v yarn >/dev/null 2>&1 && echo "  ✓ Yarn: $$(yarn --version)" || echo "  - Yarn: Not installed"
	@command -v docker >/dev/null 2>&1 && echo "  ✓ Docker: $$(docker --version | cut -d' ' -f3)" || echo "  - Docker: Not installed"
	@command -v ctags >/dev/null 2>&1 && echo "  ✓ Universal CTags: $$(ctags --version | head -1)" || echo "  - CTags: Not installed"
	@echo ""
	@echo "$(COLOR_BOLD)Project Structure:$(COLOR_RESET)"
	@[ -d "$(SRC_DIR)" ] && echo "  ✓ Backend source: $(SRC_DIR)/" || echo "  ✗ Backend source: Missing"
	@[ -d "$(FRONTEND_DIR)" ] && echo "  ✓ Frontend source: $(FRONTEND_DIR)/" || echo "  - Frontend source: Missing"
	@[ -d "$(INDEX_DIR)" ] && echo "  ✓ Code indexes: $(INDEX_DIR)/" || echo "  - Code indexes: Not generated"
	@echo ""
	@echo "$(COLOR_BOLD)Docker Status:$(COLOR_RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_DEV) ps 2>/dev/null || echo "  - Docker environment not running"

# Default target when no target is specified
.DEFAULT_GOAL := help
