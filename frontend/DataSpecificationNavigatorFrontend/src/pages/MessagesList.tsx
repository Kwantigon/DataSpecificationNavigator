import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch"
import { PlusCircle } from "lucide-react";
import { Copy } from "lucide-react";
import { useState } from "react";
/* These imports are not working for some reason.
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { oneLight } from "react-syntax-highlighter/dist/cjs/styles/prism";
*/

import type {
  Message,
  WelcomeMessage,
  UserMessage,
  ReplyMessage,
  Suggestions,
  GroupedSuggestions,
  SelectedSuggestedProperty,
  SuggestedProperty,
} from "./ConversationPage";

const isDatatypeProperty = (property: SuggestedProperty) => {
  return property.type === "DatatypeProperty";
}

// ---------------- Message type-specific components ----------------

function WelcomeMessageCard({ message }: { message: WelcomeMessage }) {
  return (
    <Card className="mb-4 bg-green-50">
      <CardContent className="p-4 space-y-2">
        <p className="text-gray-800">{message.text}</p>
        <p className="text-gray-800">Summary: {message.dataSpecificationSummary}</p>
        <p className="italic text-gray-500">Suggested: {message.suggestedFirstMessage}</p>
      </CardContent>
    </Card>
  );
}

function UserMessageCard({ message }: { message: UserMessage }) {
  return (
    <Card className="mb-4 bg-blue-50">
      <CardContent className="p-4">
        <p className="text-gray-800 text-right">{message.text}</p>
      </CardContent>
    </Card>
  );
}

interface ReplyMessageCardProps {
  message: ReplyMessage;
  selectedItemsForExpansion: SelectedSuggestedProperty[];
  onSuggestedPropertyClick: (property: SuggestedProperty, replyMsg: ReplyMessage) => void;
  onToggleSuggestedProperty: (
    property: SuggestedProperty,
    isSelected: boolean
  ) => void;
  onUpdateSuggestedPropertyOptions: (
    iri: string,
    options: { isOptional?: boolean; filterExpression?: string }
  ) => void;
  onAddAllSelected: () => void;
  currentReplyMessageId: string | null;
}

function ReplyMessageCard({
  message,
  selectedItemsForExpansion,
  onSuggestedPropertyClick: onPropertyClick,
  onToggleSuggestedProperty: onPropertyToggle,
  onUpdateSuggestedPropertyOptions: onPropertyOptionsUpdate,
  onAddAllSelected: onAddAllSelected,
  currentReplyMessageId
}: ReplyMessageCardProps) {
  const renderSuggestions = (suggestions: Suggestions) => {
    const renderGroup = (group: GroupedSuggestions, keyPrefix: string) => (
      <div key={keyPrefix} className="ml-4 mt-2">
        <p className="text-sm font-medium italic">{group.itemExpanded}:</p>
        <ul className="list-disc list-inside ml-4 mt-1">
          {group.suggestions.map((item) => {
            const existingSelection = selectedItemsForExpansion.find(
              (selected) => selected.iri === item.iri
            );
            const isSelected = !!existingSelection;
            const isFromCurrentReply = message.id === currentReplyMessageId;
            return (
              <li key={item.iri} className="flex items-center space-x-1">
                {isFromCurrentReply && (<Checkbox
                  checked={isSelected}
                  onCheckedChange={(checked) =>
                    onPropertyToggle(item, !!checked)
                  }
                  className="border-2 border-gray-700 data-[state=checked]:bg-gray-700"
                />)}
                <Button
                  variant="link"
                  onClick={() => onPropertyClick(item, message)}
                  className="p-0 h-auto text-sm text-blue-600 underline cursor-pointer"
                >
                  {item.connection}
                </Button>
                {/* Extra options when selected */}
                {isSelected && (
                  <div className="ml-6 mt-2 space-y-2">
                    {/* Optional toggle */}
                    <div className="flex items-center space-x-2">
                      <Switch
                        checked={existingSelection.isOptional}
                        onCheckedChange={(checked) =>
                          onPropertyOptionsUpdate(item.iri, {
                            isOptional: checked,
                          })
                        }
                      />
                      <span className="text-xs text-gray-600">Mark as OPTIONAL</span>
                    </div>

                    {/* Filter expression input for datatype properties */}
                    {isDatatypeProperty(item) && (
                      <Input
                        placeholder="Filter expression (e.g. {var} > 100)"
                        value={existingSelection.filterExpression ?? ""}
                        onChange={(e) =>
                          onPropertyOptionsUpdate(item.iri, {
                            filterExpression: e.target.value,
                          })
                        }
                      />
                    )}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      </div>
    );

    return (
      <div className="space-y-4">
        {suggestions.directConnections?.map((group, idx) =>
          renderGroup(group, `direct-${idx}`)
        )}

        {(suggestions.indirectConnections.length > 0) && (
          <>
            <p className="text-sm font-semibold text-gray-600 mb-2medium italic">More items to consider</p>
            {suggestions.indirectConnections?.map((group, idx) =>
              renderGroup(group, `indirect-${idx}`)
            )}
          </>
        )}
      </div>
    );
  };

  const [copied, setCopied] = useState(false);
  return (
    <Card className="mb-4 bg-gray-50">
      <CardContent className="p-4 space-y-4">
        {/* Reply text */}
        <p className="text-gray-800">{message.text}</p>

        {/* SPARQL query */}
        {message.sparqlQuery && (
          <div className="relative mb-4">
            {/* Copy button with icon, subtle styling */}
            <Button
              size="sm"
              variant="ghost"  // <-- lighter, more subtle
              className="absolute top-0 right-0 flex items-center gap-1 z-10 text-gray-600 hover:text-gray-900"
              onClick={() => {
                navigator.clipboard.writeText(message.sparqlQuery ?? "");
                setCopied(true);
                setTimeout(() => setCopied(false), 1000)
              }}
            >
              <Copy className="w-4 h-4" />
              {copied ? "Copied!" : "Copy"}
            </Button>
            <h4 className="text-sm font-semibold text-gray-600 mb-1">SPARQL query:</h4>
            <div className="mt-2">
              <pre className="whitespace-pre-wrap break-words bg-gray-100 p-3 rounded overflow-y-auto max-h-64">
                <code>{message.sparqlQuery}</code>
              </pre>
            </div>
          </div>
        )}

        {/* Suggestions */}
        {message.suggestions
          && (message.suggestions.directConnections.length > 0 || message.suggestions.indirectConnections.length > 0)
          && (
            <div>
              <h4 className="text-sm font-semibold text-gray-600 mb-2">You might want to add to your message:</h4>
              {renderSuggestions(message.suggestions)}
            </div>
          )}

        {/* Add all button */}
        {selectedItemsForExpansion.length > 0 && (
          <Button
            className="mt-4 w-full"
            onClick={onAddAllSelected}
          >
            <PlusCircle className="w-4 h-4 mr-2" />
            Add all selected items to my message
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

// ---------------- Main Messages List Component ----------------

interface MessagesListProps {
  messages: Message[];
  selectedItemsForExpansion: SelectedSuggestedProperty[];
  onSuggestedPropertyClick: (
    property: SuggestedProperty,
    replyMsg: ReplyMessage
  ) => void;
  onToggleSuggestedProperty: (
    property: SuggestedProperty,
    isSelected: boolean
  ) => void;
  onUpdateSuggestedPropertyOptions: (
    iri: string,
    options: { isOptional?: boolean; filterExpression?: string }
  ) => void;
  onAddAllSelected: () => void;
  currentReplyMessageId: string | null;
}

export default function MessagesList({
  messages,
  selectedItemsForExpansion,
  onSuggestedPropertyClick: onSuggestedPropertyClick,
  onToggleSuggestedProperty: onToggleSuggestedProperty,
  onUpdateSuggestedPropertyOptions: onUpdateSuggestedPropertyOptions,
  onAddAllSelected: onAddAllSelected,
  currentReplyMessageId
}: MessagesListProps) {
  return (
    <div className="space-y-2">
      {messages.map((msg) => {
        switch (msg.type) {
          case "WelcomeMessage":
            return <WelcomeMessageCard key={msg.id} message={msg as WelcomeMessage} />;
          case "UserMessage":
            return <UserMessageCard key={msg.id} message={msg as UserMessage} />;
          case "ReplyMessage":
            return (
              <ReplyMessageCard
                key={msg.id}
                message={msg as ReplyMessage}
                selectedItemsForExpansion={selectedItemsForExpansion}
                onSuggestedPropertyClick={onSuggestedPropertyClick}
                onToggleSuggestedProperty={onToggleSuggestedProperty}
                onUpdateSuggestedPropertyOptions={onUpdateSuggestedPropertyOptions}
                onAddAllSelected={onAddAllSelected}
                currentReplyMessageId={currentReplyMessageId}
              />
            );
          default:
            return null;
        }
      })}
    </div>
  );
}
