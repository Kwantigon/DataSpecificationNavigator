import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';

function AboutPage() {
	return (
		<div className="bg-gray-50 text-gray-900 min-h-screen flex items-center justify-center">
			<div className="max-w-[90vw] mx-auto p-8 md:p-12 text-center bg-white rounded-xl shadow-lg">
				<h1 className="text-4xl md:text-5xl font-extrabold text-blue-800 mb-6">
					Data specification navigator
				</h1>

				<p className="text-lg md:text-xl text-gray-700 mb-8 leading-relaxed">
					In an organization, there are usually only a few people who fully grasp the whole domain ontology.
					An average person might be interested in some data that is available in the organization's database
					but does not know how to construct a query to get the data they want.
				</p>

				<p className="text-lg md:text-xl text-gray-700 mb-8 leading-relaxed">
					This application will help you construct a technical query via a chat.
					You can ask questions in natural language and let the chat bot guide you through your data specification.
				</p>

				<div className="flex flex-col md:flex-row justify-center items-center space-y-4 md:space-y-0 md:space-x-6">
					<Button asChild className="px-6 py-3 rounded-lg font-semibold transition-colors duration-200 ease-in-out bg-blue-600 text-white hover:bg-blue-700 shadow-md w-full md:w-auto">
						<Link to="/manage-conversations">
							Manage existing conversations
						</Link>
					</Button>
				</div>
			</div>
		</div>
	);
}

export default AboutPage;