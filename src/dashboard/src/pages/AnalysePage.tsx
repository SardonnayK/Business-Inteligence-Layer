import { useState, useRef, useEffect } from 'react'
import { processAgentRequest } from '../api/agents'
import type { Tenant } from '../api/tenants'

interface Props {
  tenants: Tenant[]
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  tenantsLoading: boolean
  refreshTenants: () => void
}

interface Message {
  id: string
  question: string
  answer: string | null
  agentName: string | null
  error: string | null
  loading: boolean
  capability: number
}

// Render a line with **bold** markers converted to <strong>
function renderLine(line: string, key: number) {
  const parts = line.split(/(\*\*[^*]+\*\*)/)
  return (
    <span key={key}>
      {parts.map((part, i) =>
        part.startsWith('**') && part.endsWith('**')
          ? <strong key={i} className="font-semibold text-gray-900">{part.slice(2, -2)}</strong>
          : part
      )}
    </span>
  )
}

function AnalysisOutput({ text }: { text: string }) {
  const lines = text.split('\n')
  return (
    <div className="space-y-1 text-sm text-gray-700 leading-relaxed">
      {lines.map((line, i) => {
        if (line.trim() === '') return <div key={i} className="h-2" />
        // Section header lines like "**Executive Summary** —" get a slightly larger treatment
        const isSectionHeader = /^\*\*[^*]+\*\*/.test(line.trim())
        return (
          <p key={i} className={isSectionHeader ? 'mt-3 first:mt-0' : ''}>
            {renderLine(line, i)}
          </p>
        )
      })}
    </div>
  )
}

const CAPABILITY_OPTIONS = [
  { value: 1, label: 'Query', description: 'Strategic BI analysis — feasibility, risks, recommendations' },
  { value: 3, label: 'General', description: 'Broad knowledge assistant with full org context' },
]

export default function AnalysePage({
  selectedTenantId,
}: Props) {
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')
  const [capability, setCapability] = useState(1)
  const bottomRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const question = input.trim()
    if (!question || !selectedTenantId) return

    const id = crypto.randomUUID()
    const msg: Message = { id, question, answer: null, agentName: null, error: null, loading: true, capability }
    setMessages(prev => [...prev, msg])
    setInput('')

    try {
      const res = await processAgentRequest(selectedTenantId, { capability, textInput: question })
      setMessages(prev => prev.map(m =>
        m.id === id
          ? { ...m, loading: false, answer: res.output ?? '(no output)', agentName: res.agentName }
          : m
      ))
    } catch (err) {
      setMessages(prev => prev.map(m =>
        m.id === id
          ? { ...m, loading: false, error: err instanceof Error ? err.message : 'Request failed.' }
          : m
      ))
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSubmit(e as unknown as React.FormEvent)
    }
  }

  const isLoading = messages.some(m => m.loading)

  return (
    <div className="flex h-full flex-col">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-gray-200 bg-white px-8 py-4">
        <div>
          <h1 className="text-lg font-semibold text-gray-900">BI Analyst</h1>
          <p className="text-xs text-gray-500">Ask strategic questions — get structured analysis, not raw data</p>
        </div>

        {/* Capability toggle */}
        <div className="flex gap-1 rounded-lg border border-gray-200 bg-gray-50 p-1">
          {CAPABILITY_OPTIONS.map(opt => (
            <button
              key={opt.value}
              onClick={() => setCapability(opt.value)}
              title={opt.description}
              className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                capability === opt.value
                  ? 'bg-white text-blue-700 shadow-sm'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {/* Message thread */}
      <div className="flex-1 overflow-y-auto px-8 py-6 space-y-6">
        {messages.length === 0 && (
          <div className="flex flex-col items-center justify-center py-20 text-center">
            <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-50">
              <svg className="h-7 w-7 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
              </svg>
            </div>
            <p className="text-sm font-medium text-gray-700">Ask your organisation a question</p>
            <p className="mt-1 max-w-sm text-xs text-gray-400">
              The analyst synthesises your knowledge base to give structured recommendations — not just document quotes.
            </p>
            <div className="mt-5 flex flex-col gap-2 text-left w-full max-w-md">
              {[
                'Is expanding to 3 new districts this quarter feasible given our current rollout capacity?',
                'We need to let go of some technicians while growing coverage — what are the risks?',
                'What is our current SLA commitment for residential customers?',
              ].map(example => (
                <button
                  key={example}
                  onClick={() => { setInput(example); inputRef.current?.focus() }}
                  className="rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-left text-xs text-gray-600 hover:border-blue-300 hover:bg-blue-50 hover:text-blue-700 transition-colors"
                >
                  {example}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map(msg => (
          <div key={msg.id} className="space-y-3">
            {/* Question bubble */}
            <div className="flex justify-end">
              <div className="max-w-xl rounded-2xl rounded-br-sm bg-blue-600 px-4 py-3 text-sm text-white">
                {msg.question}
              </div>
            </div>

            {/* Answer bubble */}
            <div className="flex justify-start">
              <div className="max-w-3xl w-full rounded-2xl rounded-bl-sm border border-gray-200 bg-white px-5 py-4 shadow-sm">
                {msg.loading ? (
                  <div className="flex items-center gap-2 text-sm text-gray-400">
                    <Spinner className="h-4 w-4 text-blue-500" />
                    Analysing…
                  </div>
                ) : msg.error ? (
                  <p className="text-sm text-red-600">{msg.error}</p>
                ) : msg.answer ? (
                  <>
                    <AnalysisOutput text={msg.answer} />
                    {msg.agentName && (
                      <div className="mt-3 flex items-center gap-1.5 border-t border-gray-100 pt-2.5">
                        <span className="inline-flex items-center gap-1 rounded-full bg-violet-100 px-2.5 py-0.5 text-xs font-medium text-violet-700">
                          <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                            <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15M14.25 3.104c.251.023.501.05.75.082M19.8 15l-1.57.393A9.065 9.065 0 0112 15a9.065 9.065 0 00-6.23-.607L5 14.5m14.8.5l.39 1.565a2.25 2.25 0 01-2.13 2.935H5.94a2.25 2.25 0 01-2.13-2.935L4.2 16.5" />
                          </svg>
                          {msg.agentName}
                        </span>
                        <span className="text-xs text-gray-400">
                          {CAPABILITY_OPTIONS.find(o => o.value === msg.capability)?.label ?? ''}
                        </span>
                      </div>
                    )}
                  </>
                ) : null}
              </div>
            </div>
          </div>
        ))}

        <div ref={bottomRef} />
      </div>

      {/* Input */}
      <div className="border-t border-gray-200 bg-white px-8 py-4">
        {!selectedTenantId && (
          <p className="mb-2 text-xs text-amber-600">No tenant selected — log in with a tenant account first.</p>
        )}
        <form onSubmit={handleSubmit} className="flex items-end gap-3">
          <textarea
            ref={inputRef}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ask a strategic question… (Enter to send, Shift+Enter for newline)"
            rows={2}
            disabled={isLoading || !selectedTenantId}
            className="flex-1 resize-none rounded-xl border border-gray-200 px-4 py-3 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50"
          />
          <button
            type="submit"
            disabled={isLoading || !input.trim() || !selectedTenantId}
            className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isLoading
              ? <Spinner className="h-4 w-4 text-white" />
              : <SendIcon />
            }
          </button>
        </form>
      </div>
    </div>
  )
}

function Spinner({ className = 'h-4 w-4' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}

function SendIcon() {
  return (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
    </svg>
  )
}
