export interface Subscriber { id: string; email: string; timeZone: string; deliveryDay: string; deliveryHourUtc: number; weeklyEmailOptIn: boolean; }
export interface AuthResponse { accessToken: string; refreshToken: string; expiresIn: number; user: Subscriber; }
export interface Session { csrfToken: string; profile: null | { id: string; userId: string | null; revision: number; isGuest: boolean; expiresAtUtc: string | null }; guestImportAvailable: boolean; }
export interface Answers { version: 'taste-onboarding-v1'; step: string; goal: string | null; exploration: string | null; vocals: string | null; allStyles: boolean; }
export const emptyAnswers: Answers = { version: 'taste-onboarding-v1', step: 'questions', goal: null, exploration: null, vocals: null, allStyles: false };
export interface Favorite { catalogRecordId: string | null; signalId?: string | null; canonicalId?: string; name: string; artistName?: string | null; kind: string; disambiguation?: string | null; releases?: { canonicalId: string; title: string; date: string | null }[] | null; }
export interface Job { id: string; state: string; progressCode: string; errorCode?: string | null; batchId?: string | null; nextAttemptAtUtc?: string; }
export interface BatchSummary { id: string; kind: string; createdAtUtc: string; completedAtUtc: string | null; }
export interface DiscoveryPage { revision: number; favorites: Favorite[]; tags: string[]; batches: BatchSummary[]; currentJob: Job | null; nextRequestAtUtc: string | null; onboarding: Answers | null; }
export interface Overview { page: DiscoveryPage | null; styles: string[]; cooldownSeconds: number; evidenceOrigin: string; }
export type Feedback = 'None' | 'Liked' | 'Disliked' | 'AlreadyKnown';
export interface Pick { id: string; title: string; artistName: string; album: string | null; rank: number; explanation: string; matchedTags: string[]; feedback: Feedback; rating: number | null; comment: string | null; mbid: string | null; spotifyUrl: string | null; youtubeUrl: string | null; sources: string[]; }
export interface Batch { id: string; kind: string; createdAtUtc: string; coverage: { Origin?: string; Gaps?: string[] } | null; picks: Pick[]; }
export interface CatalogItem { id: string; canonicalId: string; kind: string; name: string; artistName: string | null; disambiguation: string | null; firstReleaseDate: string | null; tags: string[]; source: string; attribution: string; releases?: Favorite['releases']; }
export interface CatalogPage { items: CatalogItem[]; hasMore: boolean; normalizationVersion: string; }
export interface Lookup extends Job { items?: CatalogItem[] | null; }
export interface Tag { id: string; label: string; category: string; aliases: string[]; }
export interface TagPage { items: Tag[]; hasMore: boolean; version: string; }
export const unfinished = (job: Job | null | undefined) => !!job && ['Pending', 'Running', 'Retryable'].includes(job.state);
