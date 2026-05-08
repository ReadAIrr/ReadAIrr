import { createSelector } from 'reselect';

function createBookSelector() {
  return createSelector(
    (state, { bookId }) => bookId,
    (state, { book }) => book,
    (state) => state.books.itemMap,
    (state) => state.books.items,
    (bookId, fallbackBook, itemMap, allBooks) => {
      return allBooks[itemMap[bookId]] || fallbackBook;
    }
  );
}

export default createBookSelector;
