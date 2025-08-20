Classes that work with incoming HTTP data are separated into Controller and Service. For example, DataSpecificationController and DataSpecificationService.

The controller classes have the following responsibilities

- Validate the incoming payload's data.
- Use the payload's data to retrieve whatever is needed for the service class.
- Call the correct method on of the service class.
- Transform the return value of the service class method into the correct HTTP response.

For example, the endpoint POST /conversations will involve the ConversationController, which internally has a reference to the ConversationService. The controller will try to retrieve a data specification based on the given iri in the payload. If it does not find any such data specifications, it will return an HTTP 404 stating that the data specification was not found. Otherwise the controller will call the service's method for starting a new conversation and pass the data specification that it has retrieved. After the conversation has been successfully created, the controller will convert the relevant fields in the conversation instance into a ConversationDTO and return an HTTP 201 along with the DTO.