import { useState, useEffect, useRef } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from '@/components/ui/skeleton';
import { useParams } from "react-router-dom";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";

const BACKEND_API_URL = import.meta.env.VITE_BACKEND_API_URL;

interface UserMessage {
	id: string;
	sender: "User";
	text: string;
	timestamp: string;
	replyMessageUri?: string;
}

interface SystemMessage {
	id: string;
	sender: "System";
	text?: string; // This field should be non-empty only for the very first system's welcome message.
	timestamp: string;
	mappingText?: string;
	mappedItems?: MappedItem[];
	sparqlText?: string;
	sparqlQuery?: string;
	suggestItemsText?: string;
	suggestions?: Suggestions;
}

interface MappedItem {
	iri: string;
	label: string;
	summary: string;
	mappedWords: string;
	mappedOrSuggested: "Mapped";
}

interface SuggestedItem {
	iri: string;
	label: string;
  connection: string;
  reason: string;
  summary: string;
	mappedOrSuggested: "Suggested";
}

interface GroupedSuggestions {
  itemExpanded: string;
  suggestions: SuggestedItem[];
}

interface Suggestions {
  directConnections: GroupedSuggestions[];
  indirectConnections: GroupedSuggestions[];
}

interface SelectedSuggestedItem extends SuggestedItem {
  isOptional: boolean;
  filterExpression: string;
}

interface SubstructureDatatypeProperty {
  iri: string;
  label: string;
  domain: string;
  range: string;
	//isOptional: boolean;
	//isSelectTarget: boolean;
	//filterExpression?: string;
}

interface SubstructureObjectProperty {
  iri: string;
  label: string;
  domain: string;
  range: string;
	//isOptional: boolean;
}

interface SubstructureClass {
  iri: string;
  label: string;
  //isSelectTarget: boolean;
  objectProperties: SubstructureObjectProperty[];
  datatypeProperties: SubstructureDatatypeProperty[];
}

interface DataSpecificationSubstructure {
  classItems: SubstructureClass[];
}

// Type guard to check if a message is a ReplyMessage
const isSystemMessage = (message: UserMessage | SystemMessage): message is SystemMessage => {
	return message.sender === "System";
};
const isSuggestedItem = (item: MappedItem | SuggestedItem): item is SuggestedItem => {
	return item.mappedOrSuggested === "Suggested";
}

function ConversationPage() {
	const [messages, setMessages] = useState<(SystemMessage | UserMessage)[]>([]);
	const [currentMessage, setCurrentMessage] = useState<string>("");
	const [suggestedMessage, setSuggestedMessage] = useState<string | null>(null);
			const [selectedItemsForExpansion, setSelectedItemsForExpansion] = useState<SelectedSuggestedItem[]>([]);
	const [isSummaryDialogOpen, setIsSummaryDialogOpen] = useState<boolean>(false);
	const [selectedItemForSummary, setSelectedItemForSummary] = useState<{ item: SuggestedItem | MappedItem, parentMessageId: string } | null>(null);
	const [isFetchingMessages, setIsFetchingMessages] = useState<boolean>(true);
	const [error, setError] = useState<string | null>(null);
	const messagesEndRef = useRef<HTMLDivElement>(null);
	const { conversationId } = useParams<{ conversationId: string }>();
	const [mostRecentReplyMessageId, setMostRecentReplyMessageId] = useState<string | null>(null);
	const [mostRecentUserMessage, setMostRecentUserMessage] = useState<string | null>(null);
	const [isWaitingForReplyMessage, setIsWaitingForReplyMessage] = useState<boolean>(false);
	const [isFetchingSuggestedMessage, setIsFetchingSuggestedMessage] = useState<boolean>(false);
  const [dialogIsOptional, setDialogIsOptional] = useState<boolean>(false);
  const [dialogFilterExpression, setDialogFilterExpression] = useState<string>("");
	const [isSubstructureDialogOpen, setIsSubstructureDialogOpen] = useState<boolean>(false);
	const [dataSpecificationSubstructure, setDataSpecificationSubstructure] = useState<DataSpecificationSubstructure | null>(null);
	const [isSubstructureLoading, setIsSubstructureLoading] = useState<boolean>(false);
	const [substructureError, setSubstructureError] = useState<string | null>(null);

	const fetchMessages = async () => {
		try {
			setIsFetchingMessages(true);
			setError(null); // Clear previous error if there was one.
			const response = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}/messages`);
			if (!response.ok) {
				console.error("Failed to fetch messages.")
				console.error("Response status: " + response.status);
				console.error(response.body);
				throw new Error("Error fetching messages.");
			}
			const fetchedMessages = await response.json();
			console.log("Fetched messages in the conversation.");
			console.log(fetchedMessages);
			setMessages(fetchedMessages);

			// Look for the most recent system message.
			for (let i = fetchedMessages.length - 1; i >= 0; i--) {
				if (isSystemMessage(fetchedMessages[i])) {
					setMostRecentReplyMessageId(fetchedMessages[i].id);
					break;
				}
			}
			// Look for the most recent user message.
			for (let i = fetchedMessages.length - 1; i >= 0; i--) {
				if (!isSystemMessage(fetchedMessages[i])) {
					setMostRecentUserMessage(fetchedMessages[i].text);
					break;
				}
			}

		} catch (error) {
			console.error(error);
			setError("Failed to retrieve messages in the conversation.");
		} finally {
			setIsFetchingMessages(false);
		}
	};

	useEffect(() => {
		fetchMessages();
	}, []);

	useEffect(() => {
		if (messagesEndRef.current) {
			messagesEndRef.current.scrollTop = messagesEndRef.current.scrollHeight;
		}
	}, [messages]);

	const handleSendMessage = async () => {
		if (currentMessage.trim() === "") return;
		setError(null);

		const messageToSend = currentMessage;
		const userMessage: UserMessage = {
				id: `UserMessage-${new Date().toString()}`, // ID is generated on the back end. Will be set later.
				sender: "User",
				text: messageToSend,
				timestamp: new Date().toLocaleString()
			};

		// Add user's message to conversation
		setMessages((prevMessages) => [
			...prevMessages, userMessage
		]);

		// Clear input and suggested message
		setCurrentMessage("");
		setSuggestedMessage(null);
		setSelectedItemsForExpansion([]);
		setMostRecentUserMessage(messageToSend);

		try {
			setIsWaitingForReplyMessage(true);
			const requestBody = JSON.stringify(
					{
						textValue: userMessage.text,
						userModifiedSuggestedMessage: !(userMessage.text === suggestedMessage)
					});
			console.log("Sending a POST request with body " + requestBody);
			const postUserMsgResponse = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}/messages`, {
				method: "POST",
				headers: {
					"Content-Type": "application/json",
				},
				body: requestBody
			});

			if (!postUserMsgResponse.ok) {
				throw new Error("Failed to send user's message to the back end.");
			} else {
				console.log("User message POSTed successfully.");
			}
			const postUserMsgData = await postUserMsgResponse.json();
			console.log(`postUserMsgData: ${JSON.stringify(postUserMsgData)}`);
			userMessage.id = postUserMsgData.id; // Set the message ID that the back end generated.
			userMessage.replyMessageUri = postUserMsgData.replyMessageUri;

			// Do another fetch to get the reply to user's message.
			console.log(`Fetching a reply from ${BACKEND_API_URL}${userMessage.replyMessageUri}`);
			const getReplyMsgResponse = await fetch(`${BACKEND_API_URL}${userMessage.replyMessageUri}`);
			if (!getReplyMsgResponse.ok) {
				throw new Error("Failed to get the reply message from the back end.")
			} else {
				console.log("Reply message fetched successfully.");
			}
			const replyMsgData = await getReplyMsgResponse.json();
			console.log(`getReplyMsgData: ${JSON.stringify(replyMsgData)}`);

			setMessages((prevMessages) => [
				...prevMessages, replyMsgData
			]);
			setMostRecentReplyMessageId(replyMsgData.id);

		} catch (error) {
			console.error("Error sending message:", error);
			setMessages((prevMessages) => [
				...prevMessages,
				{
					id: `app-error-${new Date().toLocaleString()}`,
					sender: "System",
					text: "Sorry, there was some kind of error. I could not come up with a suitable answer.",
					timestamp: new Date().toLocaleString()
				}
			]);
		} finally {
			setIsWaitingForReplyMessage(false);
		}
	};

	const handleItemClick = async (item: SuggestedItem | MappedItem, parentMessageId: string) => {
		setSelectedItemForSummary({ item, parentMessageId });
		setIsSummaryDialogOpen(true);
	};

	const handleAddItemToMessage = async () => {
		if (selectedItemForSummary && isSuggestedItem(selectedItemForSummary.item)
				&& !selectedItemsForExpansion.some(item => item.iri === selectedItemForSummary.item.iri)) {

			const newItem: SelectedSuggestedItem = {
        ...selectedItemForSummary.item,
        isOptional: dialogIsOptional,
        filterExpression: dialogFilterExpression
      };

			const updatedSelectedItems = [...selectedItemsForExpansion, newItem];
			setSelectedItemsForExpansion(updatedSelectedItems);
			setIsSummaryDialogOpen(false);

			// Call back end API to get the suggested message.
			try {
				setIsFetchingSuggestedMessage(true);
				const response = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}/user-selected-items`, {
					method: "PUT",
					headers: { "Content-Type": "application/json" },
					body: JSON.stringify({
						userSelections: updatedSelectedItems.map(item => ({
							propertyIri: item.iri,
							isOptional: item.isOptional,
							filterExpression: item.filterExpression
						}))
					})
				});
				if (!response.ok) {
					throw new Error("Backend responded with an error to the suggested message request.");
				}
				const data = await response.json();
				setSuggestedMessage(data.suggestedMessage);
				setCurrentMessage(data.suggestedMessage);
			}
			catch (error) {
				console.log(error);
			} finally {
				setIsFetchingSuggestedMessage(false);
				setDialogIsOptional(false);
				setDialogFilterExpression("");
			}
		}
	};

	const handleShowSubstructure = async () => {
		setIsSubstructureDialogOpen(true);
		setIsSubstructureLoading(true);
		setSubstructureError(null);
		try {
			const response = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}/data-specification-substructure`);
			if (!response.ok) {
				throw new Error('Failed to fetch data substructure.');
			}
			const data: DataSpecificationSubstructure = await response.json();
			console.log("Got substructure data.");
			console.log(data);
			setDataSpecificationSubstructure(data);
		} catch (error) {
			console.error('Error fetching data substructure:', error);
			setSubstructureError('Could not load the data specification substructure.');
		} finally {
			setIsSubstructureLoading(false);
		}
	};

	const isSelectedItemFromMostRecentAnswer = selectedItemForSummary?.parentMessageId === mostRecentReplyMessageId;
	return (
		<div className="flex flex-col h-full p-4">
			<div ref={messagesEndRef} className="flex-1 overflow-y-auto border rounded-md p-4 space-y-4">
				{isFetchingMessages ? (
					// Display skeleton loaders while loading
					<div className="space-y-4">
						<Skeleton className="h-10 w-3/4" />
						<Skeleton className="h-10 w-1/2 ml-auto" />
						<Skeleton className="h-10 w-2/3" />
					</div>
				) : error ? (
					<div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded relative mb-4" role="alert">
						<strong className="font-bold">Error!</strong>
						<span className="block sm:inline"> {error}</span>
					</div>
				) : 
				(messages.map((msg) => (
					<div
						key={msg.id}
						className={`flex ${msg.sender === "User" ? "justify-end" : "justify-start"}`}
					>
						<Card
							className={`${
								msg.sender === "User" ? "bg-blue-100" : "bg-gray-100"
							} max-w-2xl`}
						>
							<CardContent className="p-3">
								<p className={`text-xs mt-1 ${msg.sender === "User" ? "text-right text-gray-600" : "text-left text-gray-500"}`}>
									{new Date(msg.timestamp).toLocaleString()}
								</p>

								{msg.sender === 'User' && (
									<p>{msg.text}</p>
								)}

								{isSystemMessage(msg) && (
									<>
										{/* If msg.text is available, it is the first system message. */}
										{msg.text && (<p>{msg.text}</p>)}

										{/* Mapped items */}
										{msg.mappingText && msg.mappedItems && msg.mappedItems.length > 0 && (
											<div className="mt-2">
												<p className="font-semibold text-sm">{msg.mappingText}</p>
												<ul className="list-disc list-inside mt-1">
													{msg.mappedItems.map((item) => {
														const isSelected = selectedItemsForExpansion.some(
															(selectedItem) => selectedItem.iri === item.iri
														);
														return (
															<li key={`${msg.id}-${item.iri}`} className="flex items-center space-x-1">
																<Button
																	variant="link"
																	onClick={() => handleItemClick(item, msg.id)}
																	className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer"
																>
																	{item.label}
																</Button>
																{isSelected && (
																	<svg
																		xmlns="http://www.w3.org/2000/svg"
																		className="h-4 w-4 text-green-500"
																		viewBox="0 0 20 20"
																		fill="currentColor"
																	>
																		<path
																			fillRule="evenodd"
																			d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
																			clipRule="evenodd"
																		/>
																	</svg>
																)}
															</li>
														);
													})}
												</ul>
											</div>
										)}

										{/* Sparql query */}
										{msg.sparqlText && (
											<div className="mt-2">
												<p className="font-semibold text-sm">{msg.sparqlText}</p>
												<pre className="mt-2 p-2 bg-gray-50 rounded text-sm overflow-x-auto">
													<code>{msg.sparqlQuery}</code>
												</pre>
											</div>
										)}

										{/* Suggested items */}
																				{msg.suggestions && msg.suggestions.directConnections && msg.suggestions.indirectConnections && (
											<div className="mt-4 space-y-4">
												{/* Direct Connections */}
												{msg.suggestions.directConnections.length > 0 && (
													<div>
												<p className="font-semibold text-sm">{msg.suggestItemsText}</p>
														{msg.suggestions.directConnections.map((group) => (
															<div key={group.itemExpanded} className="ml-4 mt-2">
																<p className="text-sm font-medium italic">{group.itemExpanded}:</p>
																<ul className="list-disc list-inside ml-4 mt-1">
																	{group.suggestions.map((item) => {
																		const isSelected = selectedItemsForExpansion.some(
																			(selectedItem) => selectedItem.iri === item.iri
																		);
																		return (
																			<li key={item.iri} className="flex items-center space-x-1">
																				<Button
																					variant="link"
																					onClick={() => handleItemClick(item, msg.id)}
																					className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer"
																				>
																					{item.connection}
																				</Button>
																				{isSelected && (
																					<svg
																						xmlns="http://www.w3.org/2000/svg"
																						className="h-4 w-4 text-green-500"
																						viewBox="0 0 20 20"
																						fill="currentColor"
																					>
																						<path
																							fillRule="evenodd"
																							d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
																							clipRule="evenodd"
																						/>
																					</svg>
																				)}
																			</li>
																		);
																	})}
																</ul>
															</div>
														))}
													</div>
												)}

												{/* Indirect Connections */}
												{msg.suggestions.indirectConnections.length > 0 && (
													<div className="mt-4">
														<p className="text-sm font-medium italic">More items to consider</p>
														{msg.suggestions.indirectConnections.map((group) => (
															<div key={group.itemExpanded} className="ml-4 mt-2">
																<p className="text-sm font-medium italic">{group.itemExpanded}:</p>
																<ul className="list-disc list-inside ml-4 mt-1">
																	{group.suggestions.map((item) => {
																		const isSelected = selectedItemsForExpansion.some(
																			(selectedItem) => selectedItem.iri === item.iri
																		);
																		return (
																			<li key={item.iri} className="flex items-center space-x-1">
																				<Button
																					variant="link"
																					onClick={() => handleItemClick(item, msg.id)}
																					className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer"
																				>
																					{item.connection}
																				</Button>
																				{isSelected && (
																					<svg
																						xmlns="http://www.w3.org/2000/svg"
																						className="h-4 w-4 text-green-500"
																						viewBox="0 0 20 20"
																						fill="currentColor"
																					>
																						<path
																							fillRule="evenodd"
																							d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
																							clipRule="evenodd"
																						/>
																					</svg>
																				)}
																			</li>
																		);
																	})}
																</ul>
															</div>
														))}
													</div>
												)}
											</div>
										)}
									</>
								)}
							</CardContent>
						</Card>
					</div>
				)))}
        {isWaitingForReplyMessage && (
          <div className="flex justify-start">
            <Card className="bg-gray-100 max-w-2xl">
              <CardContent className="p-3">
                <div className="h-4 w-4 rounded-full border-2 border-gray-300 border-t-blue-500 animate-spin"></div>
								<div>Thinking....</div>
								<div>This might take a minute.</div>
              </CardContent>
            </Card>
          </div>
        )}
			</div>

			{/* Suggested message */}
			{isFetchingSuggestedMessage ? (
        <Card className="mt-4 p-3 flex justify-center items-center h-16 bg-yellow-50 border-yellow-200">
          <CardContent>
						<div className="h-8 w-8 rounded-full border-4 border-gray-300 border-t-yellow-500 animate-spin mr-2"></div>
          	<p className="text-sm font-medium">Generating a suggested message...</p>
					</CardContent>
        </Card>
      ) : suggestedMessage && (
        <Card className="mt-4 p-3 bg-yellow-50 border-yellow-200">
          <CardContent className="p-0">
            <p className="text-sm font-medium">Suggested message: {suggestedMessage}</p>
          </CardContent>
        </Card>
      )}

			{mostRecentUserMessage && (
        <Card className="mt-4 p-3 bg-blue-50 border-blue-200">
          <CardContent className="p-0">
            <p className="text-sm font-medium">Your current message: {mostRecentUserMessage}</p>
          </CardContent>
        </Card>
      )}

			<div className="flex mt-4 space-x-2">
				<Input
					placeholder={suggestedMessage ? "Modify suggestion or send as is..." : "Type your message..."}
					value={currentMessage}
					onChange={(e) => setCurrentMessage(e.target.value)}
					onKeyDown={(e) => {
						if (e.key === "Enter") {
							handleSendMessage();
						}
					}}
				/>
				<Button onClick={handleSendMessage} disabled={isWaitingForReplyMessage}>SEND</Button>
				<Button 
          onClick={handleShowSubstructure} 
          disabled={isFetchingMessages || isWaitingForReplyMessage}
        >
          Show Substructure
        </Button>
			</div>

			<Dialog open={isSummaryDialogOpen} onOpenChange={setIsSummaryDialogOpen}>
				<DialogContent>
					<DialogHeader>
						<DialogTitle>Summary of "{selectedItemForSummary?.item.label}"</DialogTitle>
					</DialogHeader>
					<div className="py-4">
						{selectedItemForSummary?.item.summary && (
              <>
                <p>{selectedItemForSummary.item.summary}</p>
                {isSuggestedItem(selectedItemForSummary.item) ? (
                  <p className="mt-2 text-sm text-gray-700 font-semibold">Reason for suggestion:<br/><span className="font-normal">{selectedItemForSummary.item.reason}</span></p>
                ) : selectedItemForSummary.item.mappedWords ? (
                  <p className="mt-2 text-sm text-gray-700 font-semibold">Mapped from: <span className="font-normal">{selectedItemForSummary.item.mappedWords}</span></p>
                ) : null}
              </>
            )}
						{selectedItemForSummary && isSelectedItemFromMostRecentAnswer ? (
              // Check if the item is a suggested item
              isSuggestedItem(selectedItemForSummary.item) ? (
                // If it's a suggested item, show the button to add it to the message
                !selectedItemsForExpansion.some(item => item.iri === selectedItemForSummary.item.iri) ? (
                  <>
                    <div className="flex items-center space-x-2 mt-4">
                      <Checkbox
                        id="optional-item"
                        checked={dialogIsOptional}
                        onCheckedChange={(checked) => setDialogIsOptional(!!checked)}
                      />
                      <Label htmlFor="optional-item">Add as OPTIONAL</Label>
                    </div>
                    <div className="mt-4">
                      <Label htmlFor="filter-expression">Filter Expression</Label>
                      <Input
                        id="filter-expression"
                        placeholder="e.g., {var} > 100"
                        value={dialogFilterExpression}
                        onChange={(e) => setDialogFilterExpression(e.target.value)}
                      />
                    </div>
                    <Button onClick={() => handleAddItemToMessage()} className="mt-4">
                      Add item to my message
                    </Button>
                  </>
                ) : (
                  <p className="mt-4 text-sm text-green-600 font-semibold">
                    This item has been added to your message.
                  </p>
                )
              ) : (
                // If it's a mapped item, do not show the button.
                <></>
              )
            ) : (
              <p className="mt-4 text-sm text-gray-500 italic">
                This item cannot be used because it is not from the most recent answer.
              </p>
            )}
					</div>
				</DialogContent>
			</Dialog>

			{/* Dialog for Data Specification Substructure */}
      <Dialog open={isSubstructureDialogOpen} onOpenChange={setIsSubstructureDialogOpen}>
        <DialogContent className="max-w-3xl h-[80vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Data Specification Substructure</DialogTitle>
          </DialogHeader>
          {isSubstructureLoading ? (
            <div className="flex-1 flex items-center justify-center">
              <svg className="animate-spin -ml-1 mr-2 h-8 w-8 text-blue-500" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <p className="ml-2">Loading substructure...</p>
            </div>
          ) : substructureError ? (
            <p className="text-red-500">{substructureError}</p>
          ) : dataSpecificationSubstructure && dataSpecificationSubstructure.classItems.length > 0 ? (
            <div className="flex-1 overflow-y-auto p-4 border rounded-md bg-gray-50">
              {dataSpecificationSubstructure.classItems.map((classItem) => (
                <div key={classItem.iri} className="mb-6 p-4 border-l-4 border-blue-500 bg-white shadow-sm rounded-md">
                  <h3 className="text-lg font-bold text-blue-800 flex items-center">
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                    </svg>
                    {classItem.label}
                    {/*classItem.isSelectTarget && (
                      <span className="ml-2 text-xs font-semibold text-white bg-green-500 px-2 py-1 rounded-full">SELECT Target</span>
                    )*/}
                  </h3>
                  <p className="text-sm text-gray-500 mb-2">IRI: {classItem.iri}</p>
                  
                  {classItem.objectProperties.length > 0 && (
                    <div className="mt-4 pl-4 border-l-2 border-gray-200">
                      <h4 className="font-semibold text-gray-700">Object Properties:</h4>
                      <ul className="list-disc list-inside text-sm">
                        {classItem.objectProperties.map((prop) => (
                          <li key={prop.iri} className="mt-1">
                            <span className="font-medium text-gray-600">{prop.label}</span>:
                            <span className="ml-2 text-xs text-gray-500">({prop.domain} &rarr; {prop.range})</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {classItem.datatypeProperties.length > 0 && (
                    <div className="mt-4 pl-4 border-l-2 border-gray-200">
                      <h4 className="font-semibold text-gray-700">Datatype Properties:</h4>
                      <ul className="list-disc list-inside text-sm">
                        {classItem.datatypeProperties.map((prop) => (
                          <li key={prop.iri} className="mt-1">
                            <span className="font-medium text-gray-600">{prop.label}</span>:
                            <span className="ml-2 text-xs text-gray-500">({prop.range})</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <p className="text-gray-500">No data specification substructure to display.</p>
          )}
        </DialogContent>
      </Dialog>
		</div>
	);
}

export default ConversationPage;