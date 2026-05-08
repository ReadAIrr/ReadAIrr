import { set } from '../baseActions';

function createSetServerSideCollectionSearchHandler(section, fetchHandler) {
  return function(getState, payload, dispatch) {
    dispatch(set({ section, searchTerm: payload.searchTerm || '' }));
    dispatch(fetchHandler({ page: 1 }));
  };
}

export default createSetServerSideCollectionSearchHandler;
