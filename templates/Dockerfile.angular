# Use the official nginx image
FROM nginx:alpine

ENV artifactsDir=./artifacts

# Set the working directory
WORKDIR /usr/share/nginx/html

# Copy the built application from the artifacts directory
COPY $artifactsDir/browser .

# Expose port 80
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]