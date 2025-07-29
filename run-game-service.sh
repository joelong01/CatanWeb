#!/bin/bash

echo "======================================="
echo "Starting Catan3 Game Service"
echo "======================================="
echo ""

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/Catan3.GameService"

echo "Building project..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo ""
    echo "? Build failed! Please check the errors above."
    read -p "Press Enter to continue..."
    exit 1
fi

echo ""
echo "? Build successful! Starting service..."
echo ""

dotnet run --configuration Release

read -p "Press Enter to continue..."