import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import bookEntities from 'Book/bookEntities';
import * as commandNames from 'Commands/commandNames';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { removeItem, set, updateItem } from './baseActions';
import { executeCommand } from './commandActions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'bookFiles';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  page: 1,
  pageSize: 250,
  term: '',
  totalRecords: 0,
  totalPages: 1,
  sortKey: 'path',
  sortDirection: sortDirections.ASCENDING,

  error: null,
  isDeleting: false,
  deleteError: null,
  isSaving: false,
  saveError: null,
  items: [],

  sortPredicates: {
    quality: function(item, direction) {
      return item.quality ? item.qualityWeight : 0;
    },

    status: function(item, direction) {
      return item.review ? item.review.statusLabel : '';
    },

    candidate: function(item, direction) {
      return item.review?.candidate?.bookTitle || item.review?.parsed?.book || '';
    },

    candidateAuthor: function(item, direction) {
      return item.review?.candidate?.authorName || item.review?.parsed?.author || '';
    },

    confidence: function(item, direction) {
      return item.review?.confidence ?? -1;
    }
  },

  columns: [
    {
      name: 'select',
      columnLabel: 'Select',
      isSortable: false,
      isVisible: true,
      isModifiable: false,
      isHidden: true
    },
    {
      name: 'path',
      label: 'Path',
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'status',
      label: 'Status',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'candidate',
      label: 'Candidate Book',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'candidateAuthor',
      label: 'Candidate Author',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'confidence',
      label: 'Confidence',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'size',
      label: 'Size',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'dateAdded',
      label: 'Date Added',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'quality',
      label: 'Quality',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'actions',
      columnLabel: 'Actions',
      isVisible: true,
      isModifiable: false
    }
  ]
};

export const persistState = [
  'bookFiles.sortKey',
  'bookFiles.sortDirection',
  'bookFiles.pageSize'
];

//
// Actions Types

export const FETCH_BOOK_FILES = 'bookFiles/fetchBookFiles';
export const DELETE_BOOK_FILE = 'bookFiles/deleteBookFile';
export const DELETE_BOOK_FILES = 'bookFiles/deleteBookFiles';
export const RETRY_UNMAPPED_FILES = 'bookFiles/retryUnmappedFiles';
export const AI_REVIEW_UNMAPPED_FILES = 'bookFiles/aiReviewUnmappedFiles';
export const DEEP_IDENTIFY_UNMAPPED_FILES = 'bookFiles/deepIdentifyUnmappedFiles';
export const CLEAR_UNMAPPED_SUGGESTIONS = 'bookFiles/clearUnmappedSuggestions';
export const SET_UNMAPPED_CONTRIBUTOR_EVIDENCE = 'bookFiles/setUnmappedContributorEvidence';
export const SET_UNMAPPED_FILES_REVIEWED = 'bookFiles/setUnmappedFilesReviewed';
export const UPDATE_BOOK_FILES = 'bookFiles/updateBookFiles';
export const SET_BOOK_FILES_SORT = 'bookFiles/setBookFilesSort';
export const SET_BOOK_FILES_TABLE_OPTION = 'bookFiles/setBookFilesTableOption';
export const CLEAR_BOOK_FILES = 'bookFiles/clearBookFiles';

//
// Action Creators

export const fetchBookFiles = createThunk(FETCH_BOOK_FILES);
export const deleteBookFile = createThunk(DELETE_BOOK_FILE);
export const deleteBookFiles = createThunk(DELETE_BOOK_FILES);
export const retryUnmappedFiles = createThunk(RETRY_UNMAPPED_FILES);
export const aiReviewUnmappedFiles = createThunk(AI_REVIEW_UNMAPPED_FILES);
export const deepIdentifyUnmappedFiles = createThunk(DEEP_IDENTIFY_UNMAPPED_FILES);
export const clearUnmappedSuggestions = createThunk(CLEAR_UNMAPPED_SUGGESTIONS);
export const setUnmappedContributorEvidence = createThunk(SET_UNMAPPED_CONTRIBUTOR_EVIDENCE);
export const setUnmappedFilesReviewed = createThunk(SET_UNMAPPED_FILES_REVIEWED);
export const updateBookFiles = createThunk(UPDATE_BOOK_FILES);
export const setBookFilesSort = createAction(SET_BOOK_FILES_SORT);
export const setBookFilesTableOption = createAction(SET_BOOK_FILES_TABLE_OPTION);
export const clearBookFiles = createAction(CLEAR_BOOK_FILES);

//
// Helpers

const deleteBookFileHelper = createRemoveItemHandler(section, '/bookFile');
const fetchBookFilesHandler = createFetchHandler(section, '/bookFile');

function sanitizePage(value, fallback) {
  return typeof value === 'number' && Number.isFinite(value) ?
    Math.max(1, Math.floor(value)) :
    fallback;
}

function handleUnmappedSuggestionRequest(url, payload, dispatch) {
  const {
    bookFileIds
  } = payload;

  dispatch(batchActions([
    ...bookFileIds.map((id) => updateItem({
      section,
      id,
      isReprocessing: true,
      updateOnly: true
    })),
    set({ section, isSaving: true, saveError: null })
  ]));

  const promise = createAjaxRequest({
    url,
    method: 'POST',
    dataType: 'json',
    data: JSON.stringify({ bookFileIds })
  }).request;

  promise.done((data) => {
    dispatch(batchActions([
      ...data.map((item) => updateItem({
        section,
        ...item,
        isReprocessing: false,
        updateOnly: true
      })),

      set({
        section,
        isSaving: false,
        saveError: null
      })
    ]));
  });

  promise.fail((xhr) => {
    dispatch(batchActions([
      ...bookFileIds.map((id) => updateItem({
        section,
        id,
        isReprocessing: false,
        updateOnly: true
      })),

      set({
        section,
        isSaving: false,
        saveError: xhr
      })
    ]));
  });
}

function handleBulkDeepIdentifyRequest(payload, dispatch) {
  const {
    bookFileIds
  } = payload;

  dispatch(batchActions([
    ...bookFileIds.map((id) => updateItem({
      section,
      id,
      isReprocessing: true,
      updateOnly: true
    })),
    set({ section, isSaving: true, saveError: null })
  ]));

  const promise = createAjaxRequest({
    url: '/bookFile/unmapped/deep-identify/queue',
    method: 'POST',
    dataType: 'json',
    data: JSON.stringify({ bookFileIds })
  }).request;

  promise.done((data) => {
    dispatch(batchActions([
      ...data.map((item) => updateItem({
        section,
        ...item,
        isReprocessing: true,
        updateOnly: true
      })),

      set({
        section,
        isSaving: false,
        saveError: null
      })
    ]));

    dispatch(executeCommand({
      name: commandNames.DEEP_IDENTIFY_UNMAPPED_FILES,
      bookFileIds,
      commandFinished: () => {
        dispatch(fetchBookFiles({ unmapped: true }));
      }
    }));
  });

  promise.fail((xhr) => {
    dispatch(batchActions([
      ...bookFileIds.map((id) => updateItem({
        section,
        id,
        isReprocessing: false,
        updateOnly: true
      })),

      set({
        section,
        isSaving: false,
        saveError: xhr
      })
    ]));
  });
}

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_BOOK_FILES]: function(getState, payload = {}, dispatch) {
    if (!payload.unmapped) {
      return fetchBookFilesHandler(getState, payload, dispatch);
    }

    const state = getState()[section];
    const page = sanitizePage(payload.page, state.page || defaultState.page);
    const pageSize = sanitizePage(payload.pageSize, state.pageSize || defaultState.pageSize);
    const term = payload.term == null ? state.term || defaultState.term : payload.term;
    const refresh = payload.refresh === true;

    dispatch(set({ section, isFetching: true }));

    const { request, abortRequest } = createAjaxRequest({
      url: '/bookFile/unmapped/paged',
      data: {
        page,
        pageSize,
        term,
        refresh
      }
    });

    request.done((data) => {
      dispatch(set({
        section,
        items: data.records || [],
        page: data.page,
        pageSize: data.pageSize,
        term,
        totalRecords: data.totalRecords,
        totalPages: Math.max(Math.ceil(data.totalRecords / data.pageSize), 1),
        isFetching: false,
        isPopulated: true,
        error: null
      }));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });

    return abortRequest;
  },

  [DELETE_BOOK_FILE]: function(getState, payload, dispatch) {
    const {
      id: bookFileId,
      bookEntity: bookEntity = bookEntities.BOOKS
    } = payload;

    const bookSection = _.last(bookEntity.split('.'));
    const deletePromise = deleteBookFileHelper(getState, payload, dispatch);

    deletePromise.done(() => {
      const books = getState().books.items;
      const booksWithRemovedFiles = _.filter(books, { bookFileId });

      dispatch(batchActions([
        ...booksWithRemovedFiles.map((book) => {
          return updateItem({
            section: bookSection,
            ...book,
            bookFileId: 0,
            hasFile: false
          });
        })
      ]));
    });
  },

  [DELETE_BOOK_FILES]: function(getState, payload, dispatch) {
    const {
      bookFileIds: bookFileIds
    } = payload;

    dispatch(set({ section, isDeleting: true }));

    const promise = createAjaxRequest({
      url: '/bookFile/bulk',
      method: 'DELETE',
      dataType: 'json',
      data: JSON.stringify({ bookFileIds })
    }).request;

    promise.done(() => {
      const books = getState().books.items;
      const booksWithRemovedFiles = bookFileIds.reduce((acc, bookFileId) => {
        acc.push(..._.filter(books, { bookFileId }));

        return acc;
      }, []);

      dispatch(batchActions([
        ...bookFileIds.map((id) => {
          return removeItem({ section, id });
        }),

        ...booksWithRemovedFiles.map((book) => {
          return updateItem({
            section: 'books',
            ...book,
            bookFileId: 0,
            hasFile: false
          });
        }),

        set({
          section,
          isDeleting: false,
          deleteError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isDeleting: false,
        deleteError: xhr
      }));
    });
  },

  [RETRY_UNMAPPED_FILES]: function(getState, payload, dispatch) {
    const {
      bookFileIds
    } = payload;

    dispatch(batchActions([
      ...bookFileIds.map((id) => updateItem({
        section,
        id,
        isReprocessing: true,
        updateOnly: true
      })),
      set({ section, isSaving: true, saveError: null })
    ]));

    const promise = createAjaxRequest({
      url: '/bookFile/unmapped/retry',
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify({ bookFileIds })
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...data.map((item) => updateItem({
          section,
          ...item,
          isReprocessing: false,
          updateOnly: true
        })),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(batchActions([
        ...bookFileIds.map((id) => updateItem({
          section,
          id,
          isReprocessing: false,
          updateOnly: true
        })),

        set({
          section,
          isSaving: false,
          saveError: xhr
        })
      ]));
    });
  },

  [AI_REVIEW_UNMAPPED_FILES]: function(getState, payload, dispatch) {
    handleUnmappedSuggestionRequest('/bookFile/unmapped/ai-review', payload, dispatch);
  },

  [DEEP_IDENTIFY_UNMAPPED_FILES]: function(getState, payload, dispatch) {
    if ((payload.bookFileIds?.length ?? 0) > 1) {
      handleBulkDeepIdentifyRequest(payload, dispatch);
      return;
    }

    handleUnmappedSuggestionRequest('/bookFile/unmapped/deep-identify', payload, dispatch);
  },

  [CLEAR_UNMAPPED_SUGGESTIONS]: function(getState, payload, dispatch) {
    handleUnmappedSuggestionRequest('/bookFile/unmapped/suggestions/clear', payload, dispatch);
  },

  [SET_UNMAPPED_CONTRIBUTOR_EVIDENCE]: function(getState, payload, dispatch) {
    const {
      bookFileId,
      role = 'narrator',
      displayName
    } = payload;

    dispatch(batchActions([
      updateItem({
        section,
        id: bookFileId,
        isReprocessing: true,
        updateOnly: true
      }),
      set({ section, isSaving: true, saveError: null })
    ]));

    const promise = createAjaxRequest({
      url: '/bookFile/unmapped/contributor-evidence',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify({ bookFileId, role, displayName })
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...data.map((item) => updateItem({
          section,
          ...item,
          isReprocessing: false,
          updateOnly: true
        })),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(batchActions([
        updateItem({
          section,
          id: bookFileId,
          isReprocessing: false,
          updateOnly: true
        }),

        set({
          section,
          isSaving: false,
          saveError: xhr
        })
      ]));
    });
  },

  [SET_UNMAPPED_FILES_REVIEWED]: function(getState, payload, dispatch) {
    const {
      bookFileIds,
      reviewed = true
    } = payload;

    dispatch(set({ section, isSaving: true, saveError: null }));

    const promise = createAjaxRequest({
      url: '/bookFile/unmapped/reviewed',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify({ bookFileIds, reviewed })
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...data.map((item) => updateItem({
          section,
          ...item,
          updateOnly: true
        })),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  },

  [UPDATE_BOOK_FILES]: function(getState, payload, dispatch) {
    const {
      bookFileIds,
      quality
    } = payload;

    dispatch(set({ section, isSaving: true }));

    const requestData = {
      bookFileIds
    };

    if (quality) {
      requestData.quality = quality;
    }

    const promise = createAjaxRequest({
      url: '/bookFile/editor',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify(requestData)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...bookFileIds.map((id) => {
          const props = {};

          const bookFile = data.find((file) => file.id === id);

          props.qualityCutoffNotMet = bookFile.qualityCutoffNotMet;

          if (quality) {
            props.quality = quality;
          }

          return updateItem({ section, id, ...props });
        }),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({
  [SET_BOOK_FILES_SORT]: createSetClientSideCollectionSortReducer(section),
  [SET_BOOK_FILES_TABLE_OPTION]: createSetTableOptionReducer(section),

  [CLEAR_BOOK_FILES]: createClearReducer(section, {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: []
  })

}, defaultState, section);
