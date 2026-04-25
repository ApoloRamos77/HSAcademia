FROM nginx:alpine
# Copiamos la app estática (react build)
COPY . /usr/share/nginx/html
# Copiamos la configuración personalizada de Nginx
COPY default.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
