FROM node:20-alpine AS build
WORKDIR /app
COPY polaper-linku-web/package*.json ./
RUN npm install
COPY polaper-linku-web/ .
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
