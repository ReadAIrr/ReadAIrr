import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';

const logLevelOptions = [
  { key: 'info', value: 'Info' },
  { key: 'debug', value: 'Debug' },
  { key: 'trace', value: 'Trace' }
];

const CUSTOM_METADATA_SOURCE = 'custom';

const metadataSourceOptions = [
  { key: '', value: 'Readarr Default', hint: 'Built-in metadata source' },
  { key: 'https://api.bookinfo.pro', value: 'rreading-glasses (Goodreads)', hint: 'https://api.bookinfo.pro' },
  { key: 'https://hardcover.bookinfo.pro', value: 'rreading-glasses (Hardcover)', hint: 'https://hardcover.bookinfo.pro' },
  { key: CUSTOM_METADATA_SOURCE, value: 'Custom URL' }
];

function getMetadataSourceOption(metadataSource, metadataSourceMode) {
  if (metadataSourceMode === CUSTOM_METADATA_SOURCE) {
    return CUSTOM_METADATA_SOURCE;
  }

  const metadataSourceValue = metadataSource || '';
  const metadataSourceOption = metadataSourceOptions.find((option) => option.key === metadataSourceValue);

  return metadataSourceOption ? metadataSourceValue : CUSTOM_METADATA_SOURCE;
}

class DevelopmentSettings extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      metadataSourceMode: null
    };
  }

  //
  // Listeners

  onMetadataSourceOptionChange = ({ value }) => {
    if (value === CUSTOM_METADATA_SOURCE) {
      this.setState({ metadataSourceMode: CUSTOM_METADATA_SOURCE });

      return;
    }

    this.setState({ metadataSourceMode: null });

    this.props.onInputChange({
      name: 'metadataSource',
      value
    });
  };

  onCustomMetadataSourceChange = ({ value }) => {
    this.props.onInputChange({
      name: 'metadataSource',
      value
    });
  };

  //
  // Render

  render() {
    const {
      isFetching,
      error,
      settings,
      hasSettings,
      onInputChange,
      onSavePress,
      ...otherProps
    } = this.props;

    const metadataSource = settings.metadataSource.value || '';
    const metadataSourceOption = getMetadataSourceOption(metadataSource, this.state.metadataSourceMode);
    const isCustomMetadataSource = metadataSourceOption === CUSTOM_METADATA_SOURCE;

    return (
      <PageContent title={translate('Development')}>
        <SettingsToolbarConnector
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <div>
                Unable to load Development settings
              </div>
          }

          {
            hasSettings && !isFetching && !error &&
              <Form
                id="developmentSettings"
                {...otherProps}
              >
                <FieldSet legend={translate('MetadataProviderSource')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('MetadataSource')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="metadataSourceOption"
                      value={metadataSourceOption}
                      values={metadataSourceOptions}
                      helpText={translate('MetadataSourceHelpText')}
                      helpTextWarning={translate('MetadataSourceHelpTextWarning')}
                      helpLink="https://github.com/blampe/rreading-glasses#usage"
                      onChange={this.onMetadataSourceOptionChange}
                    />
                  </FormGroup>

                  {
                    isCustomMetadataSource &&
                      <FormGroup>
                        <FormLabel>
                          {translate('CustomMetadataSource')}
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.TEXT}
                          name="metadataSource"
                          helpText={translate('CustomMetadataSourceHelpText')}
                          helpLink="https://github.com/blampe/rreading-glasses#self-hosting"
                          onChange={this.onCustomMetadataSourceChange}
                          {...settings.metadataSource}
                        />
                      </FormGroup>
                  }
                </FieldSet>

                <FieldSet legend={translate('Logging')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('LogRotation')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="logRotate"
                      helpText={translate('LogRotateHelpText')}
                      onChange={onInputChange}
                      {...settings.logRotate}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('ConsoleLogLevel')}
                    </FormLabel>
                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="consoleLogLevel"
                      values={logLevelOptions}
                      onChange={onInputChange}
                      {...settings.consoleLogLevel}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('LogSQL')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="logSql"
                      helpText={translate('LogSqlHelpText')}
                      onChange={onInputChange}
                      {...settings.logSql}
                    />
                  </FormGroup>
                </FieldSet>

                <FieldSet legend={translate('Analytics')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('FilterAnalyticsEvents')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="filterSentryEvents"
                      helpText={translate('FilterSentryEventsHelpText')}
                      onChange={onInputChange}
                      {...settings.filterSentryEvents}
                    />
                  </FormGroup>
                </FieldSet>
              </Form>
          }
        </PageContentBody>
      </PageContent>
    );
  }

}

DevelopmentSettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default DevelopmentSettings;
