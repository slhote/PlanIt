import { useState, type KeyboardEvent } from "react";
import { MAX_TAGS_PER_WORK_ITEM } from "../types/domain";

export function TagInput({
  tags,
  onChange,
  suggestions = [],
}: {
  tags: string[];
  onChange: (tags: string[]) => void;
  suggestions?: readonly string[];
}) {
  const [draft, setDraft] = useState("");
  const atLimit = tags.length >= MAX_TAGS_PER_WORK_ITEM;

  function addTag(raw: string) {
    const tag = raw.trim().toLowerCase();
    if (!tag || atLimit || tags.includes(tag)) return;
    onChange([...tags, tag]);
    setDraft("");
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault();
      addTag(draft);
    } else if (e.key === "Backspace" && draft === "" && tags.length > 0) {
      onChange(tags.slice(0, -1));
    }
  }

  const remainingSuggestions = suggestions.filter((s) => !tags.includes(s));

  return (
    <div>
      <div className="tag-row" style={{ marginTop: 0, marginBottom: "var(--space-2)" }}>
        {tags.map((tag) => (
          <span key={tag} className="tag tag-removable">
            {tag}
            <button
              type="button"
              className="tag-remove"
              aria-label={`Remove ${tag} tag`}
              onClick={() => onChange(tags.filter((t) => t !== tag))}
            >
              <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </span>
        ))}
      </div>
      <input
        className="input"
        value={draft}
        placeholder={atLimit ? `Max ${MAX_TAGS_PER_WORK_ITEM} tags` : "Add a tag and press Enter"}
        disabled={atLimit}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={() => addTag(draft)}
      />
      {!atLimit && remainingSuggestions.length > 0 && (
        <div className="tag-row">
          {remainingSuggestions.map((s) => (
            <button key={s} type="button" className="chip-toggle" onClick={() => addTag(s)}>
              + {s}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
