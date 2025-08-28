# Data specification navigator

To do: Add some description here.

## Prerequisites

- [Docker](https://www.docker.com/)
- [Docker Compose](https://docs.docker.com/compose/)

## Installation

1. **Clone the repository**

```bash
git clone https://github.com/Kwantigon/DataSpecificationNavigator
cd DataSpecificationNavigator
```

2. **Change the default Ollama configuration**

By default, the backend will try to connect to Ollama using the following values:
- Uri: http://host.docker.internal:11434
- Model: llama3.3:70b

This default setting assumes that Ollama is listening on the host machine at port 11434. If Ollama is listening on a different address you must set its URI in the .env file. To change default settings, do the following:

```bash
cp .env.example .env
```

Then replace the dummy values with your own values.

3. **Build the docker images**

```bash
docker-compose build
```

**IMPORTANT**:

The docker-compose.yml specifies mapping of `host.docker.internal` to `host-gateway`. On Linux it means `host.docker.internal` address is likely mapped to 172.17.0.1

If Ollama is listening on 127.0.0.1:11434, then the backend *WILL NOT* be able to connect to it. Make sure Ollama listens on the correct address.

**SSH tunneling to Ollama**

If the LLM is served by Ollama on a remote server, then you must make sure there is an SSH tunnel to the remote server. The following command creates an SSH tunnel that listens on all interfaces.

```bash
ssh -f -N -L 0.0.0.0:11434:localhost:11434 user@remote-server
```

This is important because if the SSH tunnel is from 127.0.0.1:11434 to the remote server, then the backend cannot reach it from inside the container.

## Running the app

After building the images, run both the frontend and the backend.

```bash
docker-compose up
```

By default, the frontend is served at `http://localhost:8080`.

The backend is running at `http://localhost:8080/backend-api`.

You can try sending a GET requrest to `http://localhost:8080/backend-api/hello` to check that the backend is running.

### Changing the base URL and port.

You can change the base URL by setting the `BASE_URL` value in the .env file. For example, if you want the app to run at `http://localhost:8080/my/custom/path`, set `BASE_URL=/my/custom/path` in the .env file.

Similarly, you can change the default port for the app by setting the `APP_PORT` value in the .env file.
