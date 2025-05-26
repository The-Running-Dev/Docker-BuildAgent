#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Check if the token is provided
if [ -z "$gitHubPackagesToken" ]; then
  echo "GitHub Packages Token is Required..."
  exit 1
fi

# Accept image tag as an optional argument, default to 'latest' if not provided
imageTag="build-agent"
tagSuffix="${1:-latest}"
ghcrImageTag="ghcr.io/the-running-dev/${imageTag}:${tagSuffix}"
gitHubUsername='the-running-dev'

# Build the Docker image
echo "Building the Docker image with tag: $tagSuffix ..."
docker build -t $imageTag:$tagSuffix .

# Tag the Docker image for GitHub Container Registry
echo "Tagging the Docker image for GitHub Container Registry..."
docker tag $imageTag:$tagSuffix $ghcrImageTag

# Authenticate with GitHub Container Registry
echo "Authenticating with GitHub Container Registry..."
echo $gitHubPackagesToken | docker login ghcr.io -u $gitHubUsername --password-stdin

# Push the Docker image to GitHub Container Registry
echo "Pushing the Docker image to GitHub Container Registry..."
docker push $ghcrImageTag