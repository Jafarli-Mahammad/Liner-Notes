import { useState } from 'react';
import type { Feedback, Pick } from '../api/models';
import { usePersonal } from '../app/Personal';
import { Notice } from './Notice';

export function FeedbackControls({ pick, onSaved }: { pick: Pick; onSaved: (next: Pick) => void }) {
  const personal = usePersonal();
  const [rating, setRating] = useState(pick.rating?.toString() ?? ''), [comment, setComment] = useState(pick.comment ?? '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<unknown>(null), [message, setMessage] = useState('');
  async function save(feedback: Feedback, clear = false, choose = false) {
    setError(null); setMessage(''); setSaving(true);
    try {
      const incompatible = !!rating && ((feedback === 'Liked' && Number(rating) <= 3) || (feedback === 'Disliked' && Number(rating) >= 7));
      const nextFeedback = !choose && incompatible ? 'None' : feedback;
      const nextRating = clear || !rating || (choose && incompatible) ? null : Number(rating), nextComment = clear ? '' : comment;
      await personal.feedback(pick, nextFeedback, nextRating, nextComment);
      if (clear || (choose && incompatible)) setRating('');
      if (clear) setComment('');
      onSaved({ ...pick, feedback: nextFeedback, rating: nextRating, comment: nextComment || null });
      setMessage(incompatible && !clear ? choose ? 'Feedback saved. Your previous rating was cleared to match this choice.'
        : 'Feedback saved. Your previous choice was cleared to match this rating.' : 'Feedback saved.');
    } catch (e) { setError(e); } finally { setSaving(false); }
  }
  return <div className="feedback-controls" aria-busy={saving}>
    <div className="feedback-buttons" role="group" aria-label={`Feedback for ${pick.title}`}>
      {([['Liked', '♡', 'Like'], ['AlreadyKnown', '✓', 'Already know'], ['Disliked', '×', 'Not for me']] as const).map(([value, symbol, label]) =>
        <button key={value} type="button" disabled={personal.busy || saving} aria-pressed={pick.feedback === value} onClick={() => save(value, false, true)}><span aria-hidden="true">{symbol}</span>{label}</button>)}
    </div>
    <details className="rating-details"><summary>Rate or add a note</summary>
      <div className="rating-fields"><label>Rating for {pick.title}<select disabled={personal.busy || saving} value={rating} onChange={e => setRating(e.target.value)}><option value="">No rating</option>{Array.from({ length: 10 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1}/10</option>)}</select></label>
        <label>Your note <span className="field-help">Optional</span><textarea disabled={personal.busy || saving} maxLength={1000} value={comment} onChange={e => setComment(e.target.value)} /></label></div>
      <div className="action-row"><button className="button button-secondary" disabled={personal.busy || saving} onClick={() => save(pick.feedback)} type="button">Save rating and note</button><button className="text-button" disabled={personal.busy || saving} onClick={() => save('None', true)} type="button">Clear feedback</button></div>
    </details>
    {!!error && <Notice error={error} onRetry={() => { personal.reload().catch(setError); }} />}
    <p className={`feedback-status field-help${saving ? ' delayed-status' : ''}`} role="status">{saving ? 'Saving feedback…' : message}</p>
  </div>;
}
