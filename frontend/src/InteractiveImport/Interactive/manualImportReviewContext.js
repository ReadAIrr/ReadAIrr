export function getManualImportReviewContext(item) {
  if (item.review) {
    const {
      parsed = {},
      candidate = {}
    } = item.review;

    return {
      id: item.id,
      path: item.path,
      matched: {
        authorId: candidate.authorId,
        authorName: candidate.authorName,
        bookId: candidate.bookId,
        bookTitle: candidate.bookTitle,
        foreignEditionId: candidate.foreignEditionId,
        quality: item.quality,
        releaseGroup: item.releaseGroup
      },
      tags: {
        authorTitle: parsed.author,
        bookTitle: parsed.book,
        title: parsed.title,
        isbn: parsed.isbn,
        asin: parsed.asin,
        year: parsed.year,
        seriesTitle: parsed.seriesTitle,
        seriesIndex: parsed.seriesIndex
      },
      rejections: (item.review.reasons || []).map((reason) => {
        return {
          reason: reason.detail,
          type: reason.kind
        };
      })
    };
  }

  const {
    id,
    path,
    author,
    book,
    foreignEditionId,
    quality,
    releaseGroup,
    rejections = [],
    audioTags = {}
  } = item;

  return {
    id,
    path,
    matched: {
      authorId: author?.id,
      authorName: author?.authorName,
      bookId: book?.id,
      bookTitle: book?.title,
      foreignEditionId,
      quality,
      releaseGroup
    },
    tags: {
      authorTitle: audioTags.authorTitle,
      bookTitle: audioTags.bookTitle,
      title: audioTags.title,
      isbn: audioTags.isbn,
      asin: audioTags.asin,
      year: audioTags.year,
      seriesTitle: audioTags.seriesTitle,
      seriesIndex: audioTags.seriesIndex
    },
    rejections: rejections.map((rejection) => {
      return {
        reason: rejection.reason,
        type: rejection.type
      };
    })
  };
}

export function getManualImportDecision(item) {
  const reviewContext = getManualImportReviewContext(item);
  const reasons = item.review ?
    (item.review.reasons || []).map((reason) => {
      return {
        kind: reason.kind,
        label: reason.label,
        detail: reason.detail
      };
    }) :
    [];

  if (item.importError) {
    reasons.push({
      kind: 'error',
      label: 'Import failed',
      detail: item.importError
    });
  }

  if (item.review) {
    return {
      reviewContext,
      reasons,
      isImportable: !item.importError && item.review.status === 'ready'
    };
  }

  if (!item.author) {
    reasons.push({
      kind: 'missing',
      label: 'Missing author',
      detail: reviewContext.tags.authorTitle ? `Tagged author: ${reviewContext.tags.authorTitle}` : 'No author candidate is selected.'
    });
  }

  if (!item.book) {
    reasons.push({
      kind: 'missing',
      label: 'Missing book',
      detail: reviewContext.tags.bookTitle ? `Tagged book: ${reviewContext.tags.bookTitle}` : 'No book candidate is selected.'
    });
  }

  if (item.book && !item.foreignEditionId) {
    reasons.push({
      kind: 'missing',
      label: 'Missing edition',
      detail: 'No edition candidate is selected for the chosen book.'
    });
  }

  if (!item.quality) {
    reasons.push({
      kind: 'missing',
      label: 'Missing quality',
      detail: 'No quality was detected or selected.'
    });
  }

  item.rejections.forEach((rejection) => {
    reasons.push({
      kind: rejection.reason?.includes('not close enough') ? 'threshold' : 'rejection',
      label: rejection.type ? `${rejection.type} rejection` : 'Rejection',
      detail: rejection.reason
    });
  });

  return {
    reviewContext,
    reasons,
    isImportable: reasons.length === 0
  };
}
