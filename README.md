# Data specification navigator

The Data specification navigator is a tool that works with Dataspecer packages and allows you to ask questions about your data specification in plain English and, in return, provides you with the correct technical query (SPARQL) to get the data you need. To use a Dataspecer package for this tool, the package must contain a DSV file or an OWL file.

This tool was created as a fulfilment of a research project at MatFyz. All relevant documentation is in the `documentation` directory.

## Prerequisites

- [Docker](https://www.docker.com/)
- [Docker Compose](https://docs.docker.com/compose/)
- [Ollama](https://ollama.com/) or a Gemini API Key.

## Installation

1. **Clone the repository**

```bash
git clone https://github.com/Kwantigon/DataSpecificationNavigator
```
```
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
docker compose build
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

### Changing the base URL and port.

You can change the base URL by setting the `BASE_URL` value in the .env file. For example, if you want the app to run at `http://localhost:8080/my/custom/path`, set `BASE_URL=/my/custom/path` in the .env file.

Similarly, you can change the default port for the app by setting the `APP_PORT` value in the .env file.

### Local Dataspecer instance

The backend connects to the live Dataspecer instance at https://tool.dataspecer.com/

If you are running a local instance and would like to connect to that one, modify the `DATASPECER_URL` value in the .env file before building the images.

## Running the app

After building the images, run both the frontend and the backend.

```bash
docker compose up
```

By default, the frontend is served at `http://localhost:8080`.

The backend is running at `http://localhost:8080/backend-api`.

You can try sending a GET requrest to `http://localhost:8080/backend-api/hello` to check that the backend is running.

## Deploy using Gemini LLM

If you just don't have Ollama or just want to checkout the app, it is recommended to deploy using Gemini instead of the default Ollama connector. To deploy the application with Gemini you need a Gemini API key. Do the following:

1. Generate an API key in Google AI studio.
2. Clone and switch to the gemini branch in the Git repository.
```
git clone https://github.com/Kwantigon/DataSpecificationNavigator.git
```
```
cd DataSpecificationNavigator
```
```
git switch gemini
```
3. Create a file named `Gemini_api_key.txt` in the directory `DataSpecificationNavigator/backend/DataSpecificationNavigatorBackend/Secrets` and store your API key there.
```
mkdir backend/DataSpecificationNavigatorBackend/Secrets
```
```
echo “your Gemini key” > backend/DataSpecificationNavigatorBackend/Secrets/Gemini_api_key.txt
```
4. Build the docker images.
```
docker compose build
```
5. Run the app.
```
docker compose up
```
