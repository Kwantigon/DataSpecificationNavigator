FROM node:24-alpine AS build

WORKDIR /app

# Install dependencies.
COPY package*.json ./
RUN npm install

# Copy the rest of the application files.
COPY . .

ARG VITE_BASE_URL=/
ARG VITE_BACKEND_API_URL=/backend-api

# Build the project for production.
RUN npm run build

# Nginx stage.
FROM nginx:alpine AS final

# Copy built frontend into Nginx web root.
COPY --from=build /app/dist /usr/share/nginx/html

# Custom Nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
