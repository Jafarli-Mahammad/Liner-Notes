import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router';
import { Notice } from '../../components/Notice';

interface Information { operatorName: string | null; privacyContact: string | null; emailEnabled: boolean; mailProcessor: string | null; policyVersion: string; }
export function SourceLinks({ sources }: { sources: string[] }) {
  const links: Record<string, string> = { ListenBrainz: 'https://listenbrainz.org/', MusicBrainz: 'https://musicbrainz.org/doc/About/Data_License', 'Last.fm': 'https://www.last.fm/' };
  return <>{Array.from(new Set(sources)).map((source, index) => <span key={source}>{index > 0 && ' · '}{links[source] ? <a href={links[source]} target="_blank" rel="noreferrer">{source}</a> : source}</span>)}</>;
}
export default function InformationPage() {
  const { pathname } = useLocation();
  const [information, setInformation] = useState<Information | null>(null), [error, setError] = useState<unknown>(null);
  useEffect(() => {
    const abort = new AbortController();
    fetch('/api/public-information', { signal: abort.signal, cache: 'no-store' }).then(async response => {
      if (!response.ok) throw new Error('Service information could not be loaded. Please reload to try again.');
      setInformation(await response.json());
    }).catch(value => { if (!abort.signal.aborted) setError(value); });
    return () => abort.abort();
  }, []);
  const contact = information?.privacyContact;
  const contactLink = contact && /^(mailto:|https:\/\/)/.test(contact) ? contact : null;
  const operator = <p>{information?.operatorName ? `Operated by ${information.operatorName}.` : 'This local instance has no configured public operator.'} {contactLink && <a href={contactLink}>Contact the operator privately</a>}</p>;
  return <main id="main-content" className="page-container personal-main information-page" tabIndex={-1}>
    {pathname === '/privacy' ? <><h1>Privacy and your data</h1>{operator}
      <p>Policy version: {information?.policyVersion ?? '2026-11-13'}. You can browse without creating a profile. Essential cookies keep your session secure; they are not advertising cookies.</p>
      <h2>What we store</h2><p>Accounts store a username, email address, password hash and security state, delivery preferences, taste selections, saved recommendations and their score evidence, ratings and optional notes. Guest taste and history are linked to a random credential in your browser and expire after seven inactive days. Authentication credentials and tokens are excluded from downloads.</p>
      <h2>Music sources and the public feed</h2><p>Explicit searches and discovery use music providers and public metadata. Their servers receive the service's requests and network address. We do not send them your password, email address or private feedback. Public music metadata is shared; your account, taste, notes and private recommendation history are not a public profile. <Link to="/sources">Read about sources and rights</Link>.</p>
      <h2>Email</h2><p>Weekly emails require your explicit opt-in and a confirmed address. You can opt out in Account or use the unsubscribe link. Confirmation and password-recovery messages are separate account-security requests.</p>
      <p>{information?.emailEnabled ? 'Resend processes recipient addresses and email contents for delivery. Its retention and privacy terms apply to provider-held copies.' : 'External email delivery is not enabled on this instance.'} We do not add tracking pixels, collect open/click events or rewrite links for tracking.</p>
      {information?.emailEnabled && <p><a href="https://resend.com/legal/privacy-policy" target="_blank" rel="noreferrer">Resend privacy information</a></p>}
      <h2>Retention and control</h2><p>Account taste and history remain until you delete the account. Security-mail payloads expire with their links (24 hours for confirmation, one hour for recovery); weekly payloads expire after seven days. Completed mail bodies are cleared and delivery outcome records are retained for up to 90 days. Bounce/complaint suppression remains until account deletion or an explicit recovery review.</p>
      <p><Link to="/account">Account</Link> lets you download your data or delete your account; guests can download or clear their session. Shared public catalog and cache records are not private account data. An accepted email cannot be recalled from a recipient or delivery provider.</p>
      <h2>Backups and requests</h2><p>Independent encrypted backups expire under the operator's configured backup retention; deletion from the active service does not immediately erase independent backups or provider-held mail. Recovery must reapply deletions before reopening the service. Contact the operator privately for retention details or a privacy request; do not post your email, password or data export in a public issue.</p>
    </> : pathname === '/sources' ? <><h1>Music sources and rights</h1>
      <p>Candidates and similarity evidence come from <SourceLinks sources={['ListenBrainz', 'MusicBrainz', 'Last.fm']} />. Liner Notes applies deterministic scoring and feedback re-ranking. It does not own upstream similarity data or use a custom machine-learning model.</p>
      <h2>Metadata and tags</h2><p>MusicBrainz core metadata is available under CC0. Supplementary tags use CC BY-NC-SA 3.0: MusicBrainz attribution and the applicable noncommercial/share-alike conditions must be respected. Normalized tag weights are adaptations of that evidence. Software licensing does not replace data licensing.</p>
      <p><a href="https://musicbrainz.org/doc/About/Data_License">MusicBrainz licenses</a> · <a href="https://creativecommons.org/licenses/by-nc-sa/3.0/">CC BY-NC-SA 3.0</a>. ListenBrainz is the configured live recommendation source. Last.fm public use is unavailable without its separate applicable approval.</p>
      <h2>Artwork</h2><p>Artist photos retain their image-specific source, credit and license links. Recording covers use canonical release identities and the Cover Art Archive; availability there does not mean every cover has an open reuse license. Rights remain with their owners. Missing or ineligible artwork uses a placeholder.</p>
      <p><a href="https://coverartarchive.org/">Cover Art Archive</a> · <a href="https://commons.wikimedia.org/">Wikimedia Commons</a>. Contact the operator for an artwork concern. Search links to listening services are not an integration with their recommendation APIs.</p>
    </> : <><h1>Support and privacy requests</h1>{operator}
      <p>For an account or privacy request, use the private operator contact above. Never include passwords, reset links or full data exports in a public issue.</p>
      <p>For a reproducible software bug without personal information, use the <a href="https://github.com/Jafarli-Mahammad/Liner-Notes/issues">issue tracker</a>. Security vulnerabilities belong in the <a href="https://github.com/Jafarli-Mahammad/Liner-Notes/security/advisories/new">private security reporting form</a>.</p>
      {!contactLink && <p>Public support has not been configured for this local instance.</p>}
    </>}
    {!!error && <Notice error={error} />}
  </main>;
}
