FROM node:24-alpine AS build

WORKDIR /app

# Install dependencies.
COPY package*.json ./
RUN npm install

# Copy the rest of the application files.
COPY . .

ARG BASE_PATH=/
ENV VITE_BASE_PATH=$BASE_PATH

# Build the project for production.
RUN npm run build

# Nginx stage.
FROM nginx:stable-alpine AS final

# Copy built frontend into Nginx web root.
COPY --from=build /app/dist /usr/share/nginx/html

# Custom Nginx config template and entrypoint script.
COPY nginx.conf.template /etc/nginx/templates/nginx.conf.template
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/entrypoint.sh"]
