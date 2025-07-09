# ReadAIrr Docker Deployment Commands for macOS Host
# Copy and run these commands in your macOS terminal

# Navigate to your ReadAIrr project directory
cd /path/to/your/ReadAIrr/project

# Option 1: Use the deployment script (recommended)
./scripts/deploy-macos-host.sh

# Option 2: Manual deployment commands
# Stop any existing containers
docker compose -f docker-compose.dev.yml down

# Stop any other containers using port 8246
docker ps --format "table {{.ID}}\t{{.Names}}\t{{.Ports}}" | grep ":8246->" | awk '{print $1}' | xargs -r docker stop

# Build the Docker image
docker compose -f docker-compose.dev.yml build --no-cache

# Start ReadAIrr with network storage
docker compose -f docker-compose.dev.yml up -d

# Check the status
docker compose -f docker-compose.dev.yml ps

# View logs
docker compose -f docker-compose.dev.yml logs -f readairr-dev

# Option 3: One-liner deployment with port cleanup (after cd to project directory)
docker compose -f docker-compose.dev.yml down && docker ps --format "{{.ID}}" --filter "publish=8246" | xargs -r docker stop && docker compose -f docker-compose.dev.yml build --no-cache && docker compose -f docker-compose.dev.yml up -d

# Check ReadAIrr health
curl http://localhost:8246/api/v1/system/status

# Access ReadAIrr
open http://localhost:8246
