import { createSelector } from 'reselect';

function createBookSelector() {
  return createSelector(
    (state, { bookId }) => bookId,
    (state, { book }) => book,
    (state) => state.books.itemMap,
    (state) => state.books.items,
    (bookId, fallbackBook, itemMap = {}, allBooks = []) => {
      const bookIndex = itemMap[bookId];

      if (bookIndex == null) {
        return fallbackBook;
      }

      return allBooks[bookIndex] || fallbackBook;
    }
  );
}

export default createBookSelector;
