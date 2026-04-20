import { useState, useRef, useEffect } from 'react';
import { useProperties } from '../hooks/useProperties';
import { chatApi } from '../services/api';
import { Send } from 'lucide-react';

interface Message {
  role: 'user' | 'assistant';
  content: string;
}

export function ChatPage() {
  const { properties, selectedPropertyId, setSelectedPropertyId } = useProperties();
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = async () => {
    if (!input.trim() || !selectedPropertyId || loading) return;
    const userMessage = input.trim();
    setInput('');
    const newMessages: Message[] = [...messages, { role: 'user', content: userMessage }];
    setMessages(newMessages);
    setLoading(true);

    try {
      const res = await chatApi.send({
        propertyId: selectedPropertyId,
        message: userMessage,
        conversationHistory: messages,
      });
      if (res.data.success && res.data.data) {
        setMessages([...newMessages, { role: 'assistant', content: res.data.data.response }]);
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { error?: string } }; message?: string };
      const friendly = e.response?.data?.error
        ?? e.message
        ?? 'Sorry, something went wrong. Please try again.';
      setMessages([...newMessages, { role: 'assistant', content: friendly }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 48px)' }}>
      <div className="page-header">
        <h1 className="page-title">AI Chat</h1>
        <select
          className="select"
          value={selectedPropertyId}
          onChange={(e) => { setSelectedPropertyId(e.target.value); setMessages([]); }}
        >
          {properties.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      </div>

      <div className="card" style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <div style={{ flex: 1, overflowY: 'auto', padding: '16px 0' }}>
          {messages.length === 0 && (
            <div className="empty-state">
              <h3>Security Intelligence Assistant</h3>
              <p>Ask questions about incidents, patterns, vehicles, or trends for this property.</p>
              <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'center' }}>
                {[
                  'What are the most common incidents this month?',
                  'Are there any repeat offender vehicles?',
                  'Summarize the security trends for the past 30 days',
                ].map((q) => (
                  <button
                    key={q}
                    className="btn btn-secondary"
                    style={{ fontSize: 13 }}
                    onClick={() => { setInput(q); }}
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}
          {messages.map((msg, i) => (
            <div
              key={i}
              style={{
                display: 'flex',
                justifyContent: msg.role === 'user' ? 'flex-end' : 'flex-start',
                marginBottom: 12,
                padding: '0 8px',
              }}
            >
              <div
                style={{
                  maxWidth: '75%',
                  padding: '10px 14px',
                  borderRadius: 'var(--radius)',
                  background: msg.role === 'user' ? 'var(--accent)' : 'var(--bg-input)',
                  color: msg.role === 'user' ? 'white' : 'var(--text-primary)',
                  fontSize: 14,
                  lineHeight: 1.6,
                  whiteSpace: 'pre-wrap',
                }}
              >
                {msg.content}
              </div>
            </div>
          ))}
          {loading && (
            <div style={{ padding: '0 8px' }}>
              <div style={{
                display: 'inline-block',
                padding: '10px 14px',
                borderRadius: 'var(--radius)',
                background: 'var(--bg-input)',
                color: 'var(--text-muted)',
                fontSize: 14,
              }}>
                Thinking...
              </div>
            </div>
          )}
          <div ref={bottomRef} />
        </div>

        <div style={{
          display: 'flex',
          gap: 8,
          padding: '12px 0 0',
          borderTop: '1px solid var(--border)',
        }}>
          <input
            className="input"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); } }}
            placeholder="Ask about security incidents, trends, or patterns..."
            disabled={loading || !selectedPropertyId}
          />
          <button
            className="btn btn-primary"
            onClick={handleSend}
            disabled={loading || !input.trim() || !selectedPropertyId}
          >
            <Send size={16} />
          </button>
        </div>
      </div>
    </div>
  );
}
