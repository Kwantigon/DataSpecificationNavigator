import { BrowserRouter as Router, Routes, Route, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import ConversationPage from "@/pages/ConversationPage";
import ConversationManagementPage from "@/pages/ConversationManagementPage";
import AboutPage from "./pages/AboutPage";

function App() {
	return (
		<Router>
			<div className="flex flex-col h-screen">
				<header className="flex items-center justify-between p-4 border-b">
					<h1 className="text-xl font-bold">Data specification navigator</h1>
					<nav>
						<ul className="flex space-x-4">
							<li>
								<Button asChild variant="ghost">
									<Link to="/about">About</Link>
								</Button>
							</li>
							<li>
								<Button asChild variant="ghost">
									<Link to="/manage-conversations">Manage conversations</Link>
								</Button>
							</li>
						</ul>
					</nav>
				</header>
				<main className="flex-1 overflow-auto">
					<Routes>
						<Route path="/" element={<AboutPage/>}/>
						<Route path="/about" element={<AboutPage/>}/>
						<Route path="/conversation/:conversationId" element={<ConversationPage/>}/>
						<Route path="/manage-conversations" element={<ConversationManagementPage/>}/>
					</Routes>
				</main>
			</div>
		</Router>
	);
}

export default App;