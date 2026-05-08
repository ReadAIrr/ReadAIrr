import _ from 'lodash';

function normalize(value) {
  return `${value == null ? '' : value}`.toLowerCase();
}

function valueMatches(value, term) {
  if (Array.isArray(value)) {
    return value.some((item) => valueMatches(item, term));
  }

  if (value && typeof value === 'object') {
    return Object.values(value).some((item) => valueMatches(item, term));
  }

  return normalize(value).includes(term);
}

function searchCollection(items, state) {
  const term = normalize(state.searchTerm).trim();

  if (!term) {
    return items;
  }

  const {
    searchFields = [],
    searchPredicates = []
  } = state;

  if (!searchFields.length && !searchPredicates.length) {
    return items;
  }

  return items.filter((item) => {
    if (searchFields.some((field) => valueMatches(_.get(item, field), term))) {
      return true;
    }

    return searchPredicates.some((predicate) => predicate(item, term));
  });
}

export default searchCollection;
