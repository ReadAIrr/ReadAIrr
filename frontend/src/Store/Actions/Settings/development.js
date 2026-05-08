import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import { set } from '../baseActions';

//
// Variables

const section = 'settings.development';

//
// Actions Types

export const FETCH_DEVELOPMENT_SETTINGS = 'settings/development/fetchDevelopmentSettings';
export const SET_DEVELOPMENT_SETTINGS_VALUE = 'settings/development/setDevelopmentSettingsValue';
export const SAVE_DEVELOPMENT_SETTINGS = 'settings/development/saveDevelopmentSettings';
export const TEST_DEVELOPMENT_METADATA_SOURCE = 'settings/development/testDevelopmentMetadataSource';
export const TEST_OPENROUTER = 'settings/development/testOpenRouter';

//
// Action Creators

export const fetchDevelopmentSettings = createThunk(FETCH_DEVELOPMENT_SETTINGS);
export const saveDevelopmentSettings = createThunk(SAVE_DEVELOPMENT_SETTINGS);
export const testDevelopmentMetadataSource = createThunk(TEST_DEVELOPMENT_METADATA_SOURCE);
export const testOpenRouter = createThunk(TEST_OPENROUTER);
export const setDevelopmentSettingsValue = createAction(SET_DEVELOPMENT_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {},
    isTestingMetadataSource: false,
    metadataSourceTestResult: null,
    metadataSourceTestError: null,
    isTestingOpenRouter: false,
    openRouterTestResult: null,
    openRouterTestError: null
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_DEVELOPMENT_SETTINGS]: createFetchHandler(section, '/config/development'),
    [SAVE_DEVELOPMENT_SETTINGS]: createSaveHandler(section, '/config/development'),
    [TEST_DEVELOPMENT_METADATA_SOURCE]: function(getState, payload, dispatch) {
      const state = getSectionState(getState(), section, true);
      const testData = Object.assign({}, state.item, state.pendingChanges, payload);

      dispatch(set({
        section,
        isTestingMetadataSource: true,
        metadataSourceTestResult: null,
        metadataSourceTestError: null
      }));

      const promise = createAjaxRequest({
        url: '/config/development/test',
        method: 'POST',
        dataType: 'json',
        data: JSON.stringify({
          metadataSource: testData.metadataSource
        })
      }).request;

      promise.done((data) => {
        dispatch(set({
          section,
          isTestingMetadataSource: false,
          metadataSourceTestResult: data,
          metadataSourceTestError: null
        }));
      });

      promise.fail((xhr) => {
        dispatch(set({
          section,
          isTestingMetadataSource: false,
          metadataSourceTestResult: null,
          metadataSourceTestError: xhr
        }));
      });
    },

    [TEST_OPENROUTER]: function(getState, payload, dispatch) {
      const state = getSectionState(getState(), section, true);
      const testData = Object.assign({}, state.item, state.pendingChanges, payload);

      dispatch(set({
        section,
        isTestingOpenRouter: true,
        openRouterTestResult: null,
        openRouterTestError: null
      }));

      const promise = createAjaxRequest({
        url: '/config/development/openrouter/test',
        method: 'POST',
        dataType: 'json',
        data: JSON.stringify({
          openRouterEnabled: testData.openRouterEnabled,
          openRouterApiKey: testData.openRouterApiKey,
          openRouterBaseUrl: testData.openRouterBaseUrl,
          openRouterModel: testData.openRouterModel,
          openRouterTimeout: testData.openRouterTimeout
        })
      }).request;

      promise.done((data) => {
        dispatch(set({
          section,
          isTestingOpenRouter: false,
          openRouterTestResult: data,
          openRouterTestError: null
        }));
      });

      promise.fail((xhr) => {
        dispatch(set({
          section,
          isTestingOpenRouter: false,
          openRouterTestResult: null,
          openRouterTestError: xhr
        }));
      });
    }
  },

  //
  // Reducers

  reducers: {
    [SET_DEVELOPMENT_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
