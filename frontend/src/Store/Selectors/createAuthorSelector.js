import { createSelector } from 'reselect';

function createAuthorSelector() {
  return createSelector(
    (state, { authorId }) => authorId,
    (state, { author }) => author,
    (state) => state.authors.itemMap,
    (state) => state.authors.items,
    (authorId, fallbackAuthor, itemMap, allAuthors) => {
      return allAuthors[itemMap[authorId]] || fallbackAuthor;
    }
  );
}

export default createAuthorSelector;
