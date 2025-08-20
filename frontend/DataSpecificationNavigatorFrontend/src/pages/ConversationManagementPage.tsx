import { useState, useEffect } from "react";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Trash2, FolderOpen/*, PlusCircle*/ } from "lucide-react";
import { Skeleton } from '@/components/ui/skeleton';
import { useNavigate, useSearchParams } from "react-router-dom";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'; // Import Dialog components
import { Input } from '@/components/ui/input'; 
import { Label } from '@/components/ui/label';

const BACKEND_API_URL = import.meta.env.VITE_BACKEND_API_URL;
const LAST_CHOSEN_CONVERSATION_ID_STRING = "lastChosenConversationId";

interface Conversation {
	id: string;
	title: string;
	dataSpecificationName: string;
	lastUpdated: string;
}

function ConversationManagementPage() {
	const [conversations, setConversations] = useState<Conversation[]>([]);
	const [isLoading, setIsLoading] = useState<boolean>(true);
	const [error, setError] = useState<string | null>(null);
	const [isNewConversationDialogOpen, setIsNewConversationDialogOpen] = useState<boolean>(false);
	const [newConversationDataSpecIri, setNewConversationDataSpecIri] = useState<string>("");
	const [newConversationDataSpecName, setNewConversationDataSpecName] = useState<string>("");
	const [newConversationTitle, setNewConversationTitle] = useState<string>("");
	const [newConversationError, setNewConversationError] = useState<string | null>(null);
	const [isCreatingConversation, setIsCreatingConversation] = useState<boolean>(false);
	const [showNoConversationSelectedMessage, setShowNoConversationSelectedMessage] = useState<boolean>(false);
	const navigate = useNavigate();
	const [searchParams, setSearchParams] = useSearchParams();

	useEffect(() => {
		const urlParamName_uuid = "uuid";
		const urlParamName_packageName = "packageName";
		const urlParamName_newConversation = "newConversation";

		const iriFromUrl = searchParams.get(urlParamName_uuid);
		if (iriFromUrl) {
			setNewConversationDataSpecIri(iriFromUrl);
			searchParams.delete(urlParamName_uuid);
		}
		
		const dataSpecificationNameFromUrl = searchParams.get(urlParamName_packageName);
		if (dataSpecificationNameFromUrl) {
			setNewConversationDataSpecName(dataSpecificationNameFromUrl);
			searchParams.delete(urlParamName_packageName);
		}

		const newFromUrl = searchParams.get(urlParamName_newConversation);
		if (newFromUrl && newFromUrl === "true") {
			setIsNewConversationDialogOpen(true);
			searchParams.delete(urlParamName_newConversation);
		}

		setSearchParams(searchParams);
	}, [searchParams]);

	const fetchConversations = async () => {
		try {
			setIsLoading(true);
			setError(null); // Clear previous error if there was one.
			const response = await fetch(`${BACKEND_API_URL}/conversations`);
			if (!response.ok) {
				console.error("Failed to fetch conversations.")
				console.error("Response status: " + response.status);
				console.error(response.body);
				throw new Error("Error fetching conversations.");
			}
			const data = await response.json();
			setConversations(data);
		} catch (error) {
			console.error(error);
			setError("Failed to retrieve conversations.");
		} finally {
			setIsLoading(false);
		}
	};

	useEffect(() => {
		fetchConversations();
	}, []);

	const handleOpenConversation = (conversationId: string) => {
		sessionStorage.setItem(LAST_CHOSEN_CONVERSATION_ID_STRING, conversationId);
		navigate(`/conversation/${conversationId}`);
	};

	const handleCreateNewConversation = async () => {
		setIsCreatingConversation(true);
		setNewConversationError(null);
		try {
			if (!newConversationDataSpecIri || !newConversationDataSpecName || !newConversationTitle) {
				throw new Error("Some fields are missing for conversation creation.");
			}

			const response = await fetch(`${BACKEND_API_URL}/conversations`, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
				},
				body: JSON.stringify({
					conversationTitle: newConversationTitle,
					dataspecerPackageUuid: newConversationDataSpecIri,
					dataspecerPackageName: newConversationDataSpecName
				})
			});
			console.log(JSON.stringify({
					conversationTitle: newConversationTitle,
					dataSpecificationIri: newConversationDataSpecIri,
					dataSpecificationName: newConversationDataSpecName
				}));
			if (!response.ok) {
				throw new Error(`Failed to create new conversation: ${response.statusText}`);
			}

			const newConv = await response.json();
			console.log(newConv);
			setConversations(prev => [...prev, {
				id: newConv.id,
				title: newConv.title,
				dataSpecificationName: newConv.dataSpecificationName,
				lastUpdated: newConv.lastUpdated
			}].sort((a, b) => new Date(b.lastUpdated).getTime() - new Date(a.lastUpdated).getTime()));

			// Clear form fields and close dialog
			setNewConversationDataSpecIri("");
			setNewConversationDataSpecName("");
			setNewConversationTitle("");
			setIsNewConversationDialogOpen(false);

			// Navigate to the newly created conversation
			//handleOpenConversation(newConv.id);

		} catch (err) {
			console.error('Error creating new conversation:', err);
			setNewConversationError('Failed to create conversation. Please check your inputs and try again.');
		} finally {
			setIsCreatingConversation(false);
		}
	}

	const handleDeleteConversation = async (conversationId: string) => {
		try {
			// Optimistically remove the conversation from the UI.
			setError(null); // Clear previous error if there was one.
			setConversations(conversations.filter(conv => conv.id !== conversationId));

			const response = await fetch(`${BACKEND_API_URL}/conversations/${conversationId}`, { method: "DELETE" });
			if (!response.ok) {
				console.error("Failed to delete the conversation.")
				console.error("Response status: " + response.status);
				console.error(response.body);
				throw new Error("Error deleting conversation.");
			}
			if (sessionStorage.getItem(LAST_CHOSEN_CONVERSATION_ID_STRING) === conversationId) {
				sessionStorage.removeItem(LAST_CHOSEN_CONVERSATION_ID_STRING);
			}
		} catch (error) {
			console.error(error);
			setError("Failed to delete the conversation.");
			// Re-fetch conversations to revert the optimistic update.
			fetchConversations();
		}
	};

	return (
		<div className="p-4">
			{/*<div className="flex justify-between items-center mb-4">
				<h2 className="text-2xl font-bold">Manage conversations</h2>
				<Button onClick={() => setIsNewConversationDialogOpen(true)}>
					<PlusCircle className="mr-2 h-4 w-4" /> Create new conversation
				</Button>
			</div>*/}

			{showNoConversationSelectedMessage && (
				<div className="bg-blue-100 border border-blue-400 text-blue-700 px-4 py-3 rounded relative mb-4" role="alert">
					<strong className="font-bold">Warning!</strong>
					<span className="block sm:inline"> No conversation was selected. Please open an existing conversation or create a new one to start chatting.</span>
					<span className="absolute top-0 bottom-0 right-0 px-4 py-3 cursor-pointer" onClick={() => setShowNoConversationSelectedMessage(false)}>
						<svg
							className="fill-current h-6 w-6 text-blue-500"
							role="button" xmlns="http://www.w3.org/2000/svg"
							viewBox="0 0 20 20">
								<title>Close</title>
								<path d="M14.348 14.849a1.2 1.2 0 0 1-1.697 0L10 11.819l-2.651 3.029a1.2 1.2 0 1 1-1.697-1.697l2.758-3.15-2.759-3.15a1.2 1.2 0 1 1 1.697-1.697L10 8.183l2.651-3.031a1.2 1.2 0 1 1 1.697 1.697l-2.758 3.15 2.758 3.15a1.2 1.2 0 0 1 0 1.698z"/>
						</svg>
					</span>
				</div>
			)}

			{isLoading ? (
				<div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
					<Skeleton className="h-40 w-full rounded-lg" />
					<Skeleton className="h-40 w-full rounded-lg" />
					<Skeleton className="h-40 w-full rounded-lg" />
				</div>
			) : error ? (
				<div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded relative mb-4" role="alert">
					<strong className="font-bold">Error!</strong>
					<span className="block sm:inline"> {error}</span>
				</div>
			) : conversations.length === 0 ? (
				<p>No conversations found.</p>
			) : (
				<div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
					{conversations.map((conv) => (
						<Card key={conv.id}>
							<CardHeader>
								<CardTitle>{conv.title}</CardTitle>
								<p className="text-sm text-gray-500">Data specification: {conv.dataSpecificationName}</p>
								<p className="text-xs text-gray-400">Last updated: {new Date(conv.lastUpdated).toLocaleString()}</p>
							</CardHeader>
							<CardContent className="flex justify-end space-x-2">
								<Button variant="outline" size="sm" onClick={() => handleOpenConversation(conv.id)}>
									<FolderOpen className="mr-2 h-4 w-4" />Open
								</Button>
								<Button variant="destructive" size="sm" onClick={() => handleDeleteConversation(conv.id)}>
									<Trash2 className="mr-2 h-4 w-4" />Delete
								</Button>
							</CardContent>
						</Card>
					))}
				</div>
			)}

			<Dialog open={isNewConversationDialogOpen} onOpenChange={setIsNewConversationDialogOpen}>
				<DialogContent className="sm:max-w-[425px]">
					<DialogHeader>
						<DialogTitle>Create new conversation</DialogTitle>
					</DialogHeader>
					<div className="grid gap-4 py-4">
						{newConversationError && (
							<div className="bg-red-100 border border-red-400 text-red-700 px-4 py-2 rounded relative text-sm" role="alert">
								{newConversationError}
							</div>
						)}
						{/* <div className="grid grid-cols-4 items-center gap-4">
							<Label htmlFor="dataspecerIRI" className="text-right">
								Dataspecer package UUID
							</Label>
							<Input
								id="dataspecerIRI"
								value={newConversationDataSpecIri}
								onChange={(e) => setNewConversationDataSpecIri(e.target.value)}
								className="col-span-3"
								placeholder="e.g., 061e24ee-2cba-4c19-9510-7fe5278ae02c"
								disabled={true}
							/>
						</div> */}
						<div className="grid grid-cols-4 items-center gap-4">
							<Label htmlFor="dataSpecName" className="text-right">
								Dataspecer package name
							</Label>
							<Input
								id="dataSpecName"
								value={newConversationDataSpecName}
								onChange={(e) => setNewConversationDataSpecName(e.target.value)}
								className="col-span-3"
								placeholder="e.g., Badminton specification"
								disabled={isCreatingConversation}
							/>
						</div>
						<div className="grid grid-cols-4 items-center gap-4">
							<Label htmlFor="conversationTitle" className="text-right">
								Conversation title
							</Label>
							<Input
								id="conversationTitle"
								value={newConversationTitle}
								onChange={(e) => setNewConversationTitle(e.target.value)}
								className="col-span-3"
								placeholder="e.g., Query about tournaments"
								disabled={isCreatingConversation}
							/>
						</div>
					</div>
					<DialogFooter>
						<Button variant="outline" onClick={() => setIsNewConversationDialogOpen(false)}>Cancel</Button>
						<Button onClick={handleCreateNewConversation} disabled={isCreatingConversation}>
							{isCreatingConversation ? (
								<span className="flex items-center">
									<svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
										<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
										<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
									</svg>
									Creating conversation...
								</span>
							) : (
								'Create conversation'
							)}
						</Button>
					</DialogFooter>
				</DialogContent>
			</Dialog>
		</div>
	);
}

export default ConversationManagementPage;