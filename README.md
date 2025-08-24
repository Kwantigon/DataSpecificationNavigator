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
- Model: deepseek-r1:70b

This default setting assumes that Ollama is listening on the host machine at port 11434. If Ollama is listening on a different address you must do the following:

```bash
cd /backend
```

In this directory, there is a file named `usersettings.json`. Put the LLM address and the name of the model you want to use in this file. Then copy it to the directory with the backend project.

```
# After modifying the usersettings.json file
cp ./usersettings.json DataSpecificationNavigatorBackend
```

This will override the default settings.

3. **Build the docker images**

```bash
docker-compose build
```

**IMPORTANT**:

The docker-compose.yml specifies mapping of `host.docker.internal` to `host-gateway`. On Linux it means `host.docker.internal` address is likely mapped to 172.17.0.1

If Ollama is listening on 127.0.0.1:11434, then the backend *WILL NOT* be able to connect to it. Make sure Ollama listens on the correct address. There should be an environmental variable for Ollama that sets the address.

**SSH tunneling to Ollama**

If the LLM is served by Ollama on a remote server, then you must make sure there is an SSH tunnel to the remote server. The following command creates an SSH tunnel that listens on all interfaces.

```bash
ssh -f -N -L 0.0.0.0:11434:localhost:11434 user@remote-server
```

This is important because again, if the tunnel is from 127.0.0.1:11434 to the remote server, then the backend cannot reach it from inside the container.

## Running the app

After building the images, run both the frontend and the backend.

```bash
docker-compose up
```

Access the frontend at http://localhost:8081.

