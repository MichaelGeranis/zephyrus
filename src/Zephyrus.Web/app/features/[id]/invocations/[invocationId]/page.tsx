"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import type { AgentInvocationDetail } from "@/lib/types";

export default function InvocationDetailPage() {
  const { id, invocationId } = useParams<{ id: string; invocationId: string }>();
  const [invocation, setInvocation] = useState<AgentInvocationDetail | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .getAgentInvocationDetail(id, invocationId)
      .then(setInvocation)
      .finally(() => setLoading(false));
  }, [id, invocationId]);

  if (loading) return <p className="text-gray-500">Loading...</p>;
  if (!invocation) return <p className="text-red-500">Invocation not found.</p>;

  return (
    <div>
      <div className="mb-6">
        <Link
          href={`/features/${id}`}
          className="text-sm text-blue-600 hover:text-blue-800"
        >
          &larr; Back to feature
        </Link>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <h1 className="text-xl font-bold text-gray-900 capitalize">
          {invocation.agentName} Agent Invocation
        </h1>
        <p className="text-xs text-gray-400 mt-1">
          {new Date(invocation.invokedAt).toLocaleString()} &middot; {invocation.durationMs}ms
        </p>
      </div>

      <PromptSection title="System Prompt" content={invocation.systemPrompt} />
      <PromptSection title="User Message" content={invocation.userMessage} />
      <PromptSection title="Response" content={invocation.response} />
    </div>
  );
}

function PromptSection({ title, content }: { title: string; content: string }) {
  return (
    <div className="mb-6">
      <h2 className="text-lg font-semibold text-gray-900 mb-2">{title}</h2>
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        <pre className="text-sm text-gray-800 whitespace-pre-wrap break-words font-mono">
          {content}
        </pre>
      </div>
    </div>
  );
}
