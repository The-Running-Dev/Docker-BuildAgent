#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Check if the token is provided
if [ -z "$gitHubPackagesToken" ]; then
  echo "GitHub Packages Token is Required..."
  
  exit 1
fi

imageTag='build-agent'
ghcrImageTag="ghcr.io/the-running-dev/${imageTag}:latest"
gitHubUsername='the-running-dev'

# Build the Docker image
echo "Building the Docker image..."
docker build -t $imageTag .

# Tag the Docker image for GitHub Container Registry
echo "Tagging the Docker image for GitHub Container Registry..."
docker tag $imageTag $ghcrImageTag

# Authenticate with GitHub Container Registry
echo "Authenticating with GitHub Container Registry..."
echo $gitHubPackagesToken | docker login ghcr.io -u $gitHubUsername --password-stdin

# Push the Docker image to GitHub Container Registry
echo "Pushing the Docker image to GitHub Container Registry..."
docker push $ghcrImageTag