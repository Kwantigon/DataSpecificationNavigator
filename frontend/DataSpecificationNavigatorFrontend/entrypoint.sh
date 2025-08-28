#!/bin/sh
set -e

envsubst '${BASE_PATH}' < /etc/nginx/templates/nginx.conf.template > /etc/nginx/conf.d/default.conf

exec nginx -g "daemon off;"
