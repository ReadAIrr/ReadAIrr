import { createAction } from 'redux-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'series';

export const filters = [
  {
    key: 'all',
    label: 'All',
    filters: []
  },
  {
    key: 'missing',
    label: 'Missing',
    filters: [
      {
        key: 'missingBooks',
        value: 0,
        type: filterTypes.GREATER_THAN
      }
    ]
  },
  {
    key: 'complete',
    label: 'Complete',
    filters: [
      {
        key: 'missingBooks',
        value: 0,
        type: filterTypes.EQUAL
      },
      {
        key: 'totalBooks',
        value: 0,
        type: filterTypes.GREATER_THAN
      }
    ]
  },
  {
    key: 'monitored',
    label: 'Monitored',
    filters: [
      {
        key: 'monitored',
        value: true,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'unmonitored',
    label: 'Unmonitored',
    filters: [
      {
        key: 'monitored',
        value: false,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'crossAuthor',
    label: 'Cross-Author',
    filters: [
      {
        key: 'authorCount',
        value: 1,
        type: filterTypes.GREATER_THAN
      }
    ]
  }
];

function getCompletenessValue(item, name) {
  const completeness = item.completeness || {};

  return completeness[name] || 0;
}

function getAuthorNames(item) {
  const books = item.books || [];
  const names = books.map((book) => book.authorName).filter(Boolean);

  return [...new Set(names)];
}

function getAuthorCount(item) {
  return getAuthorNames(item).length;
}

function isSeriesMonitored(item) {
  const books = item.books || [];

  return books.length > 0 && books.every((book) => book.monitored);
}

export const sortPredicates = {
  authorCount: function(item) {
    return getAuthorCount(item);
  },

  availableBooks: function(item) {
    return getCompletenessValue(item, 'availableBooks');
  },

  missingBooks: function(item) {
    return getCompletenessValue(item, 'missingBooks');
  },

  monitored: function(item) {
    return isSeriesMonitored(item) ? 1 : 0;
  },

  totalBooks: function(item) {
    return getCompletenessValue(item, 'totalBooks');
  }
};

export const filterPredicates = {
  author: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return getAuthorNames(item).some((authorName) => predicate(authorName, filterValue));
  },

  authorCount: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return predicate(getAuthorCount(item), filterValue);
  },

  availableBooks: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return predicate(getCompletenessValue(item, 'availableBooks'), filterValue);
  },

  missingBooks: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return predicate(getCompletenessValue(item, 'missingBooks'), filterValue);
  },

  monitored: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return predicate(isSeriesMonitored(item), filterValue);
  },

  totalBooks: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    return predicate(getCompletenessValue(item, 'totalBooks'), filterValue);
  }
};

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  sortKey: 'title',
  sortDirection: sortDirections.ASCENDING,
  secondarySortKey: 'title',
  secondarySortDirection: sortDirections.ASCENDING,
  selectedFilterKey: 'all',
  searchTerm: '',
  view: 'overview',
  items: [],

  columns: [
    {
      name: 'title',
      label: 'Title',
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'authors',
      label: 'Authors',
      isSortable: false,
      isVisible: true
    },
    {
      name: 'totalBooks',
      label: 'Books',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'availableBooks',
      label: 'Available',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'missingBooks',
      label: 'Missing',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'monitored',
      columnLabel: 'Monitored',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'authorCount',
      label: 'Author Count',
      isSortable: true,
      isVisible: false
    },
    {
      name: 'unmonitoredBooks',
      label: 'Unmonitored Books',
      isSortable: false,
      isVisible: false
    },
    {
      name: 'actions',
      columnLabel: 'Actions',
      isVisible: true,
      isModifiable: false
    }
  ],

  filters,
  filterPredicates,
  searchPredicates: [
    function(item, term) {
      const completeness = item.completeness || {};
      const books = item.books || [];
      const values = [
        item.title,
        item.description,
        completeness.missingBooks ? `${completeness.missingBooks} missing` : 'complete',
        ...books.flatMap((book) => [
          book.title,
          book.authorName,
          book.missingReason,
          book.hasFile ? 'available' : 'missing'
        ])
      ];

      return values.some((value) => `${value || ''}`.toLowerCase().includes(term));
    }
  ],
  filterBuilderProps: [
    {
      name: 'title',
      label: 'Title',
      type: filterBuilderTypes.STRING
    },
    {
      name: 'author',
      label: 'Author',
      type: filterBuilderTypes.STRING
    },
    {
      name: 'monitored',
      label: 'Monitored',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'totalBooks',
      label: 'Books',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'availableBooks',
      label: 'Available',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'missingBooks',
      label: 'Missing',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'authorCount',
      label: 'Author Count',
      type: filterBuilderTypes.NUMBER
    }
  ],
  sortPredicates
};

export const persistState = [
  'series.sortKey',
  'series.sortDirection',
  'series.selectedFilterKey',
  'series.customFilters',
  'series.view',
  'series.columns'
];

//
// Actions Types

export const FETCH_SERIES = 'series/fetchSeries';
export const SET_SERIES_SORT = 'series/setSeriesSort';
export const SET_SERIES_FILTER = 'series/setSeriesFilter';
export const SET_SERIES_SEARCH_TERM = 'series/setSeriesSearchTerm';
export const SET_SERIES_VIEW = 'series/setSeriesView';
export const SET_SERIES_TABLE_OPTION = 'series/setSeriesTableOption';
export const CLEAR_SERIES = 'series/clearSeries';
export const SET_SERIES_VALUE = 'series/setSeriesValue';

//
// Action Creators

export const fetchSeries = createThunk(FETCH_SERIES);
export const setSeriesSort = createAction(SET_SERIES_SORT);
export const setSeriesFilter = createAction(SET_SERIES_FILTER);
export const setSeriesSearchTerm = createAction(SET_SERIES_SEARCH_TERM);
export const setSeriesView = createAction(SET_SERIES_VIEW);
export const setSeriesTableOption = createAction(SET_SERIES_TABLE_OPTION);
export const clearSeries = createAction(CLEAR_SERIES);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_SERIES]: createFetchHandler(section, '/series')
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_SERIES_SORT]: createSetClientSideCollectionSortReducer(section),

  [SET_SERIES_FILTER]: createSetClientSideCollectionFilterReducer(section),

  [SET_SERIES_SEARCH_TERM]: function(state, { payload }) {
    return {
      ...state,
      searchTerm: payload.searchTerm || ''
    };
  },

  [SET_SERIES_VIEW]: function(state, { payload }) {
    return Object.assign({}, state, { view: payload.view });
  },

  [SET_SERIES_TABLE_OPTION]: createSetTableOptionReducer(section),

  [SET_SERIES_VALUE]: createSetSettingValueReducer(section),

  [CLEAR_SERIES]: (state) => {
    return Object.assign({}, state, {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: []
    });
  }

}, defaultState, section);
