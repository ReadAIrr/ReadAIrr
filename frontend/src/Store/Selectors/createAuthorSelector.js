import { createSelector } from 'reselect';

function createAuthorSelector() {
  return createSelector(
    (state, { authorId }) => authorId,
    (state, { author }) => author,
    (state) => state.authors.itemMap,
    (state) => state.authors.items,
    (authorId, fallbackAuthor, itemMap = {}, allAuthors = []) => {
      const authorIndex = itemMap[authorId];

      if (authorIndex == null) {
        return fallbackAuthor;
      }

      return allAuthors[authorIndex] || fallbackAuthor;
    }
  );
}

export default createAuthorSelector;
