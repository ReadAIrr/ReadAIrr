const INTRO_AUTHOR_PHRASES = [
  /\s*,?\s*read\s+for\s+you\s+by\b.*$/i,
  /\s*,?\s*read\s+by\b.*$/i,
  /\s*,?\s*narrated\s+by\b.*$/i,
  /\s*,?\s*performed\s+by\b.*$/i,
  /\s*,?\s*by\s+the\s+author\b.*$/i
];

const PUBLISHER_CONTEXT_PATTERNS = [
  /^audible$/i,
  /^audible\s+(studios|originals?)$/i,
  /^podium\s+audio$/i,
  /^podium\s+audio\s+presents\b/i,
  /^.+\s+audio\s+presents\b/i
];

function normalizeCandidate(value) {
  return (value || '')
    .replace(/[“”]/g, '"')
    .replace(/[‘’]/g, '\'')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^[\s"'`]+|[\s"'`]+$/g, '');
}

function isPublisherContext(value) {
  return PUBLISHER_CONTEXT_PATTERNS.some((pattern) => pattern.test(value));
}

function cleanCandidateAuthor(value) {
  let candidate = normalizeCandidate(value);

  if (!candidate || isPublisherContext(candidate)) {
    return '';
  }

  INTRO_AUTHOR_PHRASES.forEach((pattern) => {
    candidate = candidate.replace(pattern, '');
  });

  candidate = normalizeCandidate(candidate);

  if (!candidate || isPublisherContext(candidate)) {
    return '';
  }

  return candidate;
}

function cleanCandidateBook(value) {
  const candidate = normalizeCandidate(value);

  if (!candidate || isPublisherContext(candidate)) {
    return '';
  }

  return candidate;
}

function firstClean(values, cleaner) {
  for (const value of values) {
    const cleaned = cleaner(value);

    if (cleaned) {
      return cleaned;
    }
  }

  return '';
}

export function getCleanAuthorCandidate(review, suggestion) {
  const candidate = review?.candidate || {};

  if (candidate.authorId) {
    return '';
  }

  return firstClean([
    suggestion?.likelyAuthor,
    candidate.authorName,
    review?.parsed?.author
  ], cleanCandidateAuthor);
}

export function getCleanBookCandidate(review, suggestion) {
  const candidate = review?.candidate || {};

  if (candidate.bookId) {
    return '';
  }

  return firstClean([
    suggestion?.likelyBook,
    candidate.bookTitle,
    review?.parsed?.book,
    review?.parsed?.title
  ], cleanCandidateBook);
}

export function getBookContext(review, suggestion) {
  return firstClean([
    suggestion?.likelyBook,
    review?.candidate?.bookTitle,
    review?.parsed?.book
  ], cleanCandidateBook);
}

export function getAuthorContext(review, suggestion) {
  return firstClean([
    suggestion?.likelyAuthor,
    review?.candidate?.authorName,
    review?.parsed?.author
  ], cleanCandidateAuthor);
}

export function buildAddSearchUrl({ term, contextType, contextBook, contextAuthor, contextNarrator }) {
  const params = [
    `term=${encodeURIComponent(term)}`,
    `returnUrl=${encodeURIComponent('/unmapped')}`,
    `returnLabel=${encodeURIComponent('Unmapped Files')}`
  ];

  if (contextType) {
    params.push(`contextType=${encodeURIComponent(contextType)}`);
  }

  if (contextBook) {
    params.push(`contextBook=${encodeURIComponent(contextBook)}`);
  }

  if (contextAuthor) {
    params.push(`contextAuthor=${encodeURIComponent(contextAuthor)}`);
  }

  if (contextNarrator) {
    params.push(`contextNarrator=${encodeURIComponent(contextNarrator)}`);
  }

  return `/add/search?${params.join('&')}`;
}
