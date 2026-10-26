import { useState } from 'react';
import type { Feedback, Pick } from '../api/models';
import { usePersonal } from '../app/Personal';
import { Notice } from './Notice';

export function FeedbackControls({ pick, onSaved }: { pick: Pick; onSaved: (next: Pick) => void }) {
  const personal = usePersonal();
  const [rating, setRating] = useState(pick.rating?.toString() ?? ''), [comment, setComment] = useState(pick.comment ?? '');
  const [error, setError] = useState<unknown>(null), [message, setMessage] = useState('');
  async function save(feedback: Feedback, clear = false) {
    setError(null); setMessage('');
    try {
      const nextRating = clear || !rating ? null : Number(rating), nextComment = clear ? '' : comment;
      await personal.feedback(pick, feedback, nextRating, nextComment);
      if (clear) { setRating(''); setComment(''); }
      onSaved({ ...pick, feedback, rating: nextRating, comment: nextComment || null }); setMessage('Feedback saved.');
    } catch (e) { setError(e); }
  }
  return <div className="feedback-controls">
    <div className="feedback-buttons" role="group" aria-label={`Feedback for ${pick.title}`}>
      {([['Liked', '♡', 'Like'], ['AlreadyKnown', '✓', 'Already know'], ['Disliked', '×', 'Not for me']] as const).map(([value, symbol, label]) =>
        <button key={value} type="button" disabled={personal.busy} aria-pressed={pick.feedback === value} onClick={() => save(value)}><span aria-hidden="true">{symbol}</span>{label}</button>)}
    </div>
    <details className="rating-details"><summary>Rate or add a note</summary>
      <div className="rating-fields"><label>Rating for {pick.title}<select value={rating} onChange={e => setRating(e.target.value)}><option value="">No rating</option>{Array.from({ length: 10 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1}/10</option>)}</select></label>
        <label>Your note <span className="field-help">Optional</span><textarea maxLength={1000} value={comment} onChange={e => setComment(e.target.value)} /></label></div>
      <div className="action-row"><button className="button button-secondary" disabled={personal.busy} onClick={() => save(pick.feedback)} type="button">Save rating and note</button><button className="text-button" disabled={personal.busy} onClick={() => save('None', true)} type="button">Clear feedback</button></div>
    </details>
    {!!error && <Notice error={error} onRetry={() => { personal.reload().catch(setError); }} />}
    <p className="sr-only" role="status">{message}</p>
  </div>;
}
