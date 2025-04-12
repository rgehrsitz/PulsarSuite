#!/bin/bash
# Setup environment for Pulsar/Beacon testing

echo "Setting up environment for Pulsar/Beacon testing..."

# Check if Redis is running
if ! command -v redis-cli &> /dev/null || ! redis-cli ping &> /dev/null; then
    echo "Redis server not running. Attempting to start with Docker..."
    
    # Check if Docker is available
    if ! command -v docker &> /dev/null; then
        echo "Error: Docker not found. Please install Docker or start Redis manually."
        exit 1
    fi
    
    # Check if Redis container is already there but not running
    if docker ps -a | grep -q "pulsar-redis"; then
        echo "Redis container exists. Starting it..."
        docker start pulsar-redis
    else
        echo "Creating new Redis container..."
        docker run -d --name pulsar-redis -p 6379:6379 redis:latest
    fi
    
    # Wait for Redis to be ready
    echo "Waiting for Redis to be ready..."
    for i in {1..10}; do
        if redis-cli ping &> /dev/null; then
            echo "Redis is ready!"
            break
        fi
        if [ $i -eq 10 ]; then
            echo "Error: Redis failed to start properly."
            exit 1
        fi
        sleep 1
    done
else
    echo "Redis is already running."
fi

# Clear any existing test data
echo "Clearing existing Redis data..."
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del
redis-cli keys "state:*" | xargs -r redis-cli del

echo "Environment setup complete!"