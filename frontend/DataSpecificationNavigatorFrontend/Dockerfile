FROM node:20-alpine AS build

# Create and set the current working directory.
WORKDIR /app

# Copy package.json and package-lock.json
COPY package*.json ./

# Install dependencies.
RUN npm install

# Copy the rest of the application files.
COPY . .

# Build the project for production.
RUN npm run build

# Serve the static files with Nginx
# Use a lightweight Nginx image
FROM nginx:stable-alpine AS final

# Copy the built files from the 'build' stage into the Nginx public directory
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port 80 to the host.
EXPOSE 80

# Start Nginx.
CMD ["nginx", "-g", "daemon off;"]
