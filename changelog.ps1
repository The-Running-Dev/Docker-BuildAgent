#!/usr/bin/env pwsh

& docker run `
    --rm `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    forge-build --change-log-source 'all'