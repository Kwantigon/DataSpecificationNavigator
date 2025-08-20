import { useState, useEffect, useRef } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
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
  domain: string;
  suggestions: SuggestedItem[];
}

interface Suggestions {
  directConnections: GroupedSuggestions[];
  indirectConnections: GroupedSuggestions[];
}

interface SubstructureDatatypeProperty {
  Iri: string;
  Label: string;
  Domain: string;
  Range: string;
}

interface SubstructureObjectProperty {
  Iri: string;
  Label: string;
  Domain: string;
  Range: string;
}

interface SubstructureClass {
  Iri: string;
  Label: string;
  IsSelectTarget: boolean;
  ObjectProperties: SubstructureObjectProperty[];
  DatatypeProperties: SubstructureDatatypeProperty[];
}

interface DataSpecificationSubstructure {
  ClassItems: SubstructureClass[];
}

// A new interface to represent the items selected for expansion
interface SelectedItemPayload {
  iri: string;
  isOptional: boolean;
  filterExpression: string;
}

interface SelectedSuggestedItem extends SuggestedItem {
  isOptional: boolean;
  filterExpression: string;
}


// Type guard to check if a message is a ReplyMessage
const isSystemMessage = (message: UserMessage | SystemMessage): message is SystemMessage => {
	return message.sender === "System";
};
const isSuggestedItem = (item: MappedItem | SuggestedItem): item is SuggestedItem => {
	return item.mappedOrSuggested === "Suggested";
}

const mockMessages: (UserMessage | SystemMessage)[] = [
  {
    id: "system-1",
    sender: "System",
    text: "Welcome to the conversation!",
    timestamp: "2025-08-10T10:00:00Z",
  },
  {
    id: "system-2",
    sender: "System",
    timestamp: "2025-08-10T10:01:00Z",
    mappingText: "Here are some mapped items:",
    mappedItems: [
      {
        iri: "http://example.org/item1",
        label: "Item One",
        summary: "This is a summary of item one.",
        mappedWords: "item one",
        mappedOrSuggested: "Mapped",
      },
      {
        iri: "http://example.org/item2",
        label: "Item Two",
        summary: "This is a summary of item two.",
        mappedWords: "item two",
        mappedOrSuggested: "Mapped",
      },
    ],
    sparqlText: "SPARQL query for mapped items:",
    sparqlQuery: "SELECT ?s WHERE { ?s ?p ?o }",
    suggestItemsText: "You might also like these suggested items:",
    suggestions: {
      directConnections: [
        {
          domain: "Domain A",
          suggestions: [
            {
							iri: "qw1",
							label: "Item label",
              connection: "→ property1 → {Range}",
              reason: "Because it relates to your query",
              summary: "Summary of connection 1",
							mappedOrSuggested: "Suggested"
            },
            {
							iri: "qw2",
							label: "Item label",
              connection: "→ property1 → {Range}",
              reason: "Strong relevance in the same field",
              summary: "Summary of connection 3",
							mappedOrSuggested: "Suggested"
            },
          ],
        },
        {
          domain: "Domain C",
          suggestions: [
            {
							iri: "qw3",
							label: "Item label",
              connection: "← property4 ← {Domain}",
              reason: "Directly associated with mapped item",
              summary: "Summary of connection 4",
							mappedOrSuggested: "Suggested"
            },
          ],
        },
      ],
      indirectConnections: [
        {
          domain: "Domain B",
          suggestions: [
            {
							iri: "qw4",
							label: "Item label",
              connection: "← property4 ← {Domain}",
              reason: "Indirectly related via other concepts",
              summary: "Summary of connection 2",
							mappedOrSuggested: "Suggested"
            },
            {
							iri: "qw5",
              connection: "→ property1 → {Range}",
							label: "Item label",
              reason: "Linked through secondary relationships",
              summary: "Summary of connection 5",
							mappedOrSuggested: "Suggested"
            },
          ],
        },
        {
          domain: "Domain D",
          suggestions: [
            {
							iri: "qw6",
              connection: "→ property1 → {Range}",
							label: "Item label",
              reason: "Less direct but still relevant",
              summary: "Summary of connection 6",
							mappedOrSuggested: "Suggested"
            },
            {
							iri: "qw7",
              connection: "→ property1 → {Range}",
							label: "Item label",
              reason: "Indirect association based on usage patterns",
              summary: "Summary of connection 7",
							mappedOrSuggested: "Suggested"
            },
          ],
        },
      ],
    },
  },
  {
    id: "user-1",
    sender: "User",
    text: "Can you explain more about Item One?",
    timestamp: "2025-08-10T10:02:00Z",
    replyMessageUri: "system-2",
  },
  {
    id: "system-3",
    sender: "System",
    timestamp: "2025-08-10T10:03:00Z",
    mappingText: "More details on Item One:",
    mappedItems: [
      {
        iri: "http://example.org/item1",
        label: "Item One",
        summary: "Extended summary and details about item one.",
        mappedWords: "item one",
        mappedOrSuggested: "Mapped",
      },
    ],
  },
	{
    id: "system-4",
    sender: "System",
    text: "",
    timestamp: "0001-01-01T00:00:00",
    mappingText: "I have identified the following items from your data specification which play a role in your question.",
    mappedItems: [
        {
            iri: "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#informační-systém-veřejné-správy",
            label: "Informační systém veřejné správy",
            summary: "An Information System of Public Administration (ISVS) is a functional unit or its part, designed to ensure purposeful and systematic information activities for public administration or other state functions. Such a system comprises organized data for processing and access, operational data, and technical/programmatic tools. It serves as a comprehensive framework for managing information within the public sector.",
            mappedWords: "systems",
            mappedOrSuggested: "Mapped"
        },
        {
            iri: "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
            label: "Nahrazení úředně ověřeného podpisu nebo uznávaného elektronického podpisu",
            summary: "This class signifies the replacement of an officially verified handwritten signature or a recognized electronic signature. This act is fulfilled by using an electronic signature on a document that is inextricably linked with a record from a public administration information system (ISVS). This record is further secured with a qualified electronic seal and time stamp, confirming electronic identification.",
            mappedWords: "electronic signatures",
            mappedOrSuggested: "Mapped"
        }
    ],
    sparqlText: "I have formulated a Sparql query for your question:",
    sparqlQuery: "SELECT ?informační_systém_veřejné_správy\r\nWHERE {\r\n\t?informační_systém_veřejné_správy a https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#informační-systém-veřejné-správy .\r\n\r\n\t?nahrazení_úředně_ověřeného_podpisu_nebo_uznávaného_elektronického_podpisu a https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu .\r\n\r\n}\r\n",
    suggestItemsText: "I found some items which could expand your question.",
    suggestions: {
        directConnections: [
            {
                domain: "Informační systém veřejné správy",
                suggestions: [
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq1",
												label: "labl",
                        connection: "→ Má identifikátor ISVS → http://www.w3.org/2001/XMLSchema#string",
                        reason: "To provide specific identification for the 'systems' that are the focus of your question. This adds a crucial detail about each identified ISVS.",
                        summary: "This property provides a unique identifier for an Information System of Public Administration (ISVS). This identifier is assigned within the Register of Rights and Obligations (RPP), ensuring clear and unambiguous identification. It allows for precise referencing and tracking of individual ISVS entities."
                    },
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq2",
												label: "labl",
                        connection: "→ název ISVS → https://ofn.gov.cz/zdroj/základní-datové-typy/2020-07-01/text",
                        reason: "To provide the common name for the 'systems' identified, making the results more understandable and user-friendly.",
                        summary: "This property specifies the official name of an Information System of Public Administration (ISVS). The name is typically used by the administrator of the ISVS. It provides a human-readable label for easier identification and reference of the system."
                    }
                ]
            },
            {
                domain: "Nahrazení úředně ověřeného podpisu nebo uznávaného elektronického podpisu",
                suggestions: [
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq3",
												label: "labl",
                        connection: "→ datum a čas nahrazení úředně ověřeného podpisu nebo uznávaného elektronického podpisu → http://www.w3.org/2001/XMLSchema#dateTimeStamp",
                        reason: "To add temporal context to the electronic signature event, allowing you to see when these processes occurred within the involved systems.",
                        summary: "This property records the precise date and time when the replacement of an officially verified signature or recognized electronic signature occurred. It captures the moment of the digital verification event. This timestamp is crucial for auditing and tracking the validity of the electronic act."
                    },
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq4",
												label: "labl",
                        connection: "→ žadatel → Fyzická osoba ve vztahu k nahrazení úředně ověřeného podpisu nebo uznávaného elektronického podpisu",
                        reason: "To identify the individual (applicant) associated with each electronic signature event, providing crucial insight into who initiated the process.",
                        summary: "This property identifies the physical person who acts as the applicant in the context of replacing an officially verified or recognized electronic signature. This is the individual for whom the signature replacement is being performed. It establishes a direct link to the human actor involved in the digital transaction."
                    },
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq5",
												label: "labl",
                        connection: "→ provádí OVM → Orgán veřejné moci",
                        reason: "To identify the specific public authority that performs the electronic signature replacement, adding a layer of organizational context to the systems involved.",
                        summary: "This property indicates which Public Authority (Orgán veřejné moci) is responsible for carrying out the replacement of an officially verified signature or recognized electronic signature. It links the signature event to the specific administrative body overseeing the process. This helps in understanding the organizational context of the digital transaction."
                    },
                    {
												mappedOrSuggested: "Suggested",
												iri: "qq6",
												label: "labl",
                        connection: "→ nahrazení pro účely úkonu → Digitální úkon",
                        reason: "To understand the specific digital service or 'act' for which the electronic signature replacement was needed, adding purpose and context to the signature event.",
                        summary: "This property links the replacement of an officially verified signature or recognized electronic signature to the specific Digital Act (Digitální úkon) for which it was performed. It clarifies the context and purpose of the signature replacement within a broader administrative service. This connection helps in understanding the function of the signature in a digital workflow."
                    }
                ]
            }
        ],
        indirectConnections: [
            {
                domain: "Orgán veřejné moci",
                suggestions: [
                    {
												mappedOrSuggested: "Suggested",
												iri: "ww1",
												label: "labl",
                        connection: "→ Má název orgánu veřejné moci → https://ofn.gov.cz/zdroj/základní-datové-typy/2020-07-01/text",
                        reason: "Once you identify a public authority via 'provádí-ovm', this property allows you to retrieve its human-readable name, providing a more complete picture.",
                        summary: "This property specifies the full name of a Public Authority (Orgán veřejné moci). It provides a descriptive label for the entity responsible for public administration functions. This name helps in clearly identifying the specific authority involved in various processes."
                    }
                ]
            },
            {
                domain: "Digitální úkon",
                suggestions: [
                    {
												mappedOrSuggested: "Suggested",
												iri: "ww2",
												label: "labl",
                        connection: "→ Má název úkonu služby → https://ofn.gov.cz/zdroj/základní-datové-typy/2020-07-01/text",
                        reason: "Once you identify a digital act via 'nahrazení-pro-účely-úkonu', this property allows you to retrieve its human-readable name, further clarifying the purpose of the electronic signature.",
                        summary: "This property specifies the title or name of a Digital Act (Digitální úkon). This name provides a concise description of the electronic action or service being performed. It is essential for human readability and understanding the nature of the digital transaction."
                    }
                ]
            }
        ]
    }
}
];

const mockSubstructure: DataSpecificationSubstructure = {
  "ClassItems": [
    {
      "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#fyzická-osoba-ve-vztahu-k-nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
      "Label": "Fyzická osoba ve vztahu k nahrazení úředně ověřeného podpisu nebo uznávaného elektronického podpisu",
      "IsSelectTarget": true,
      "ObjectProperties": [
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#je-autentizována",
          "Label": "je autentizována",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#fyzická-osoba-ve-vztahu-k-nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
          "Range": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#elektronická-identifikace"
        }
      ],
      "DatatypeProperties": [
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#jméno-fyzické-osoby",
          "Label": "jméno fyzické osoby",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#fyzická-osoba-ve-vztahu-k-nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
          "Range": "xsd:string"
        },
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#další-jméno-fyzické-osoby",
          "Label": "další jméno fyzické osoby",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#fyzická-osoba-ve-vztahu-k-nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
          "Range": "xsd:string"
        },
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#datum-narození",
          "Label": "datum narození",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#fyzická-osoba-ve-vztahu-k-nahrazení-úředně-ověřeného-podpisu-nebo-uznávaného-elektronického-podpisu",
          "Range": "xsd:date"
        }
      ]
    },
    {
      "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#adresní-místo",
      "Label": "adresní místo",
      "IsSelectTarget": true,
      "ObjectProperties": [],
      "DatatypeProperties": [
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#kód-adresního-místa",
          "Label": "kód adresního místa",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#adresní-místo",
          "Range": "xsd:integer"
        }
      ]
    },
    {
      "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#elektronická-identifikace",
      "Label": "Elektronická identifikace",
      "IsSelectTarget": false,
      "ObjectProperties": [
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#má-úroveň-záruky",
          "Label": "má úroveň záruky",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#prostředek-pro-elektronickou-identifikaci",
          "Range": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#úroveň-záruky"
        }
      ],
      "DatatypeProperties": [
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#datum-a-čas-elektronické-identifikace",
          "Label": "datum a čas elektronické identifikace",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#elektronická-identifikace",
          "Range": "xsd:dateTimeStamp"
        },
        {
          "Iri": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#identifikátor-elektronické-identifikace",
          "Label": "identifikátor elektronické identifikace",
          "Domain": "https://ofn.gov.cz/záznam-informačního-systému-nahrazující-úředně-ověřený-podpis/draft#elektronická-identifikace",
          "Range": "xsd:string"
        }
      ]
    }
  ]
};

function ConversationPageMock() {
	const [messages, setMessages] = useState<(SystemMessage | UserMessage)[]>(mockMessages);
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
	const [summaryError, setSummaryError] = useState<string | null>(null);
	const [mostRecentUserMessage, setMostRecentUserMessage] = useState<string | null>(null);
	const [isWaitingForReplyMessage, setIsWaitingForReplyMessage] = useState<boolean>(false);
	const [isSubstructureDialogOpen, setIsSubstructureDialogOpen] = useState<boolean>(false);
  const [dataSubstructure, setDataSubstructure] = useState<DataSpecificationSubstructure | null>(mockSubstructure);
  const [isSubstructureLoading, setIsSubstructureLoading] = useState<boolean>(false);
  const [substructureError, setSubstructureError] = useState<string | null>(null);
	const [isFetchingSuggestedMessage, setIsFetchingSuggestedMessage] = useState<boolean>(false);
  const [dialogIsOptional, setDialogIsOptional] = useState<boolean>(false);
  const [dialogFilterExpression, setDialogFilterExpression] = useState<string>("");

	const fetchMessages = async () => {
		setIsFetchingMessages(false);
		setMostRecentReplyMessageId("system-4");
		setMostRecentUserMessage("Can you provide more on wind power?");
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

	/*useEffect(() => {
			const fetchSubstructure = async () => {
				if (isSubstructureDialogOpen && conversationId && conversationId !== 'new') {
					setIsSubstructureLoading(true);
					setSubstructureError(null);
					try {
						const response = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}/data-specification-substructure`);
						if (!response.ok) {
							throw new Error('Failed to fetch data substructure.');
						}
						const data: DataSpecificationSubstructure = await response.json();
						setDataSubstructure(data);
					} catch (error) {
						console.error('Error fetching data substructure:', error);
						setSubstructureError('Could not load the data specification substructure.');
					} finally {
						setIsSubstructureLoading(false);
					}
				}
			};

			fetchSubstructure();
		}, [isSubstructureDialogOpen, conversationId]);*/

	const handleItemClick = async (item: SuggestedItem | MappedItem, parentMessageId: string) => {
		setSelectedItemForSummary({ item, parentMessageId });
		setIsSummaryDialogOpen(true);
		if (item.summary) {
			setSummaryError(null);
		} else {
			setSummaryError('Failed to load item summary. Please try again.');
		}
	};

	const handleAddItemToMessage = () => {
		if (selectedItemForSummary && isSuggestedItem(selectedItemForSummary.item) && !selectedItemsForExpansion.some(item => item.iri === selectedItemForSummary.item.iri)) {
			const newItem: SelectedSuggestedItem = {
        ...selectedItemForSummary.item,
        isOptional: dialogIsOptional,
        filterExpression: dialogFilterExpression
      };

			const updatedSelectedItems = [...selectedItemsForExpansion, newItem];
			setSelectedItemsForExpansion(updatedSelectedItems);
			setIsSummaryDialogOpen(false);

			const payload: SelectedItemPayload[] = updatedSelectedItems.map(item => ({
        iri: item.iri,
        isOptional: item.isOptional,
        filterExpression: item.filterExpression
      }));
			console.log("Added items to message payload: " + JSON.stringify(payload));
			setDialogIsOptional(false);

			//setIsFetchingSuggestedMessage(true);

			// Call back end API to get the suggested message.
			/*fetch(`${BACKEND_API_URL}/conversations/${conversationId}/user-selected-items`, {
				method: "PUT",
				headers: { "Content-Type": "application/json" },
				body: JSON.stringify({
					itemIriList: updatedSelectedItems.map(item => item.iri)
				})
			})
			.then(res => res.json())
			.then(data => {
				setSuggestedMessage(data.suggestedMessage);
				setCurrentMessage(data.suggestedMessage);
			})
			.catch(error => console.error("Error generating suggested message:", error));*/
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
							<CardContent className="p-3 break-words whitespace-normal">
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

										{/* New Suggestions Rendering */}
										{msg.suggestions && msg.suggestions.directConnections && msg.suggestions.indirectConnections && (
											<div className="mt-4 space-y-4">
												{/* Direct Connections */}
												{msg.suggestions.directConnections.length > 0 && (
													<div>
												<p className="font-semibold text-sm">{msg.suggestItemsText}</p>
														{msg.suggestions.directConnections.map((group) => (
															<div key={group.domain} className="ml-4 mt-2">
																<p className="text-sm font-medium italic">{group.domain}:</p>
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
																					className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer whitespace-normal break-words max-w-full"
																				>
																					{item.connection}
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
																				</Button>
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
															<div key={group.domain} className="ml-4 mt-2">
																<p className="text-sm font-medium italic">{group.domain}:</p>
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
																					className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer whitespace-normal break-words max-w-full"
																				>
																					{item.connection}
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
																				</Button>
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

			{/*suggestedMessage && (
				<Card className="mt-4 p-3 bg-yellow-50 border-yellow-200">
					<CardContent className="p-0">
						<p className="text-sm font-medium">Suggested message: {suggestedMessage}</p>
					</CardContent>
				</Card>
			)*/}

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
          onClick={() => setIsSubstructureDialogOpen(true)} 
          disabled={isFetchingMessages || isWaitingForReplyMessage}
        >
          Show Substructure
        </Button>
			</div>

			<Dialog open={isSummaryDialogOpen} onOpenChange={setIsSummaryDialogOpen}>
				<DialogContent>
					<DialogHeader>
						<DialogTitle>Summary of "{ selectedItemForSummary?.item.label }"</DialogTitle>
						{summaryError && (
							<DialogDescription className="text-red-500">
								Error: {summaryError}
							</DialogDescription>
						)}
					</DialogHeader>
					<div className="py-4">
						{selectedItemForSummary?.item.summary && !summaryError && (
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
          ) : dataSubstructure && dataSubstructure.ClassItems.length > 0 ? (
            <div className="flex-1 overflow-y-auto p-4 border rounded-md bg-gray-50">
              {dataSubstructure.ClassItems.map((classItem) => (
                <div key={classItem.Iri} className="mb-6 p-4 border-l-4 border-blue-500 bg-white shadow-sm rounded-md">
                  <h3 className="text-lg font-bold text-blue-800 flex items-center">
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
                    </svg>
                    {classItem.Label}
                    {classItem.IsSelectTarget && (
                      <span className="ml-2 text-xs font-semibold text-white bg-green-500 px-2 py-1 rounded-full">SELECT Target</span>
                    )}
                  </h3>
                  <p className="text-sm text-gray-500 mb-2">IRI: {classItem.Iri}</p>
                  
                  {classItem.ObjectProperties.length > 0 && (
                    <div className="mt-4 pl-4 border-l-2 border-gray-200">
                      <h4 className="font-semibold text-gray-700">Object Properties:</h4>
                      <ul className="list-disc list-inside text-sm">
                        {classItem.ObjectProperties.map((prop) => (
                          <li key={prop.Iri} className="mt-1">
                            <span className="font-medium text-gray-600">{prop.Label}</span>:
                            <span className="ml-2 text-xs text-gray-500">({prop.Domain} &rarr; {prop.Range})</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {classItem.DatatypeProperties.length > 0 && (
                    <div className="mt-4 pl-4 border-l-2 border-gray-200">
                      <h4 className="font-semibold text-gray-700">Datatype Properties:</h4>
                      <ul className="list-disc list-inside text-sm">
                        {classItem.DatatypeProperties.map((prop) => (
                          <li key={prop.Iri} className="mt-1">
                            <span className="font-medium text-gray-600">{prop.Label}</span>:
                            <span className="ml-2 text-xs text-gray-500">({prop.Range})</span>
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

export default ConversationPageMock;