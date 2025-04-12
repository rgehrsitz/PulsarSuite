#!/bin/bash

# PulsarSuite Build Script
# Usage: ./build-project.sh [ProjectName]

set -e

# Default project name
PROJECT_NAME=${1:-DefaultProject}

# Directory structure
SOLUTION_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/.."
SRC_DIR="${SOLUTION_DIR}/src"
RULES_DIR="${SRC_DIR}/Rules"
TESTS_DIR="${SRC_DIR}/Tests"
BIN_DIR="${SRC_DIR}/Bin"
OUTPUT_DIR="${SOLUTION_DIR}/output"
DIST_DIR="${OUTPUT_DIR}/dist"
REPORTS_DIR="${OUTPUT_DIR}/reports"

# Project directories
PROJECT_RULES_DIR="${RULES_DIR}/${PROJECT_NAME}"
PROJECT_TESTS_DIR="${TESTS_DIR}/${PROJECT_NAME}"
PROJECT_BIN_DIR="${BIN_DIR}/${PROJECT_NAME}"
PROJECT_DIST_DIR="${DIST_DIR}/${PROJECT_NAME}"
PROJECT_REPORTS_DIR="${REPORTS_DIR}/${PROJECT_NAME}"

# Tool paths
PULSAR_PATH="${SOLUTION_DIR}/Pulsar"
BEACON_TESTER_PATH="${SOLUTION_DIR}/BeaconTester"

# Create directories if they don't exist
mkdir -p "${PROJECT_TESTS_DIR}"
mkdir -p "${PROJECT_BIN_DIR}"
mkdir -p "${PROJECT_DIST_DIR}"
mkdir -p "${PROJECT_REPORTS_DIR}"

# Check if project rules directory exists
if [ ! -d "${PROJECT_RULES_DIR}" ]; then
    echo "Error: Project rules directory not found: ${PROJECT_RULES_DIR}"
    exit 1
fi

# Display build information
echo "Building project: ${PROJECT_NAME}"
echo "Rules directory: ${PROJECT_RULES_DIR}"
echo "Output directory: ${PROJECT_DIST_DIR}"

# Step 1: Validate rules
echo "Step 1: Validating rules..."
cd "${PULSAR_PATH}"
dotnet run --project Pulsar.Compiler/Pulsar.Compiler.csproj validate "${PROJECT_RULES_DIR}"

# Step 2: Compile rules
echo "Step 2: Compiling rules..."
cd "${PULSAR_PATH}"
dotnet run --project Pulsar.Compiler/Pulsar.Compiler.csproj compile "${PROJECT_RULES_DIR}" -o "${PROJECT_BIN_DIR}"

# Step 3: Generate tests
echo "Step 3: Generating tests..."
cd "${BEACON_TESTER_PATH}"
dotnet run generate "${PROJECT_BIN_DIR}" -o "${PROJECT_TESTS_DIR}"

# Step 4: Build Beacon application
echo "Step 4: Building Beacon application..."
cd "${PULSAR_PATH}"
dotnet run --project Pulsar.Compiler/Pulsar.Compiler.csproj build "${PROJECT_BIN_DIR}" -o "${PROJECT_DIST_DIR}"

# Step 5: Run tests
echo "Step 5: Running tests..."
cd "${BEACON_TESTER_PATH}"
dotnet run run "${PROJECT_TESTS_DIR}" -o "${PROJECT_REPORTS_DIR}"

echo "Build completed successfully!"
echo "Distributable Beacon application: ${PROJECT_DIST_DIR}"
echo "Test reports: ${PROJECT_REPORTS_DIR}"
