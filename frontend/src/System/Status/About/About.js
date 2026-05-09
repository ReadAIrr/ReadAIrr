import PropTypes from 'prop-types';
import React, { Component } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import FieldSet from 'Components/FieldSet';
import Label from 'Components/Label';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { kinds } from 'Helpers/Props';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import StartTime from './StartTime';
import styles from './About.css';

function getReadinessKind(readinessState) {
  switch (readinessState) {
    case 'activePostgreSQL':
    case 'reachable':
      return kinds.SUCCESS;
    case 'configured':
      return kinds.INFO;
    case 'blocked':
      return kinds.WARNING;
    default:
      return kinds.DEFAULT;
  }
}

function getMetadataReadinessKind(readinessState) {
  switch (readinessState) {
    case 'reachable':
      return kinds.SUCCESS;
    case 'blocked':
      return kinds.WARNING;
    default:
      return kinds.INFO;
  }
}

class About extends Component {

  //
  // Render

  render() {
    const {
      version,
      packageVersion,
      packageAuthor,
      isNetCore,
      isDocker,
      runtimeVersion,
      databaseVersion,
      databaseType,
      migrationVersion,
      appData,
      startupPath,
      mode,
      startTime,
      timeFormat,
      longDateFormat,
      databaseStatus,
      metadataServiceStatus,
      isFetchingMetadataServiceStatus,
      metadataServiceStatusError
    } = this.props;
    const postgres = databaseStatus?.postgres || {};

    return (
      <>
        <FieldSet legend={translate('About')}>
          <DescriptionList className={styles.descriptionList}>
            <DescriptionListItem
              title={translate('Version')}
              data={version}
            />

            {
              packageVersion &&
                <DescriptionListItem
                  title={translate('PackageVersion')}
                  data={(packageAuthor ? <span> {packageVersion} {' by '} <InlineMarkdown data={packageAuthor} /> </span> : packageVersion)}
                />
            }

            {
              isNetCore &&
                <DescriptionListItem
                  title={translate('NETCore')}
                  data={`Yes (${runtimeVersion})`}
                />
            }

            {
              isDocker &&
                <DescriptionListItem
                  title={translate('Docker')}
                  data={'Yes'}
                />
            }

            <DescriptionListItem
              title={translate('Database')}
              data={`${titleCase(databaseType)} ${databaseVersion}`}
            />

            <DescriptionListItem
              title={translate('DatabaseMigration')}
              data={migrationVersion}
            />

            <DescriptionListItem
              title={translate('AppDataDirectory')}
              data={appData}
            />

            <DescriptionListItem
              title={translate('StartupDirectory')}
              data={startupPath}
            />

            <DescriptionListItem
              title={translate('Mode')}
              data={titleCase(mode)}
            />

            <DescriptionListItem
              title={translate('Uptime')}
              data={
                <StartTime
                  startTime={startTime}
                  timeFormat={timeFormat}
                  longDateFormat={longDateFormat}
                />
              }
            />
          </DescriptionList>
        </FieldSet>

        {
          databaseStatus &&
            <FieldSet legend="Database Migration">
              <DescriptionList className={styles.descriptionList}>
                <DescriptionListItem
                  title="Active database"
                  data={databaseStatus.activeProvider}
                />

                <DescriptionListItem
                  title="Readiness"
                  data={
                    <Label kind={getReadinessKind(databaseStatus.readinessState)}>
                      {databaseStatus.readinessLabel}
                    </Label>
                  }
                />

                {
                  databaseStatus.sqlitePath &&
                    <DescriptionListItem
                      title="SQLite database"
                      data={`${databaseStatus.sqlitePath}${databaseStatus.sqliteSizeLabel ? ` (${databaseStatus.sqliteSizeLabel})` : ''}`}
                    />
                }

                <DescriptionListItem
                  title="PostgreSQL configured"
                  data={postgres.isConfigured ? `Yes (${postgres.host}:${postgres.port})` : 'No'}
                />

                {
                  postgres.isConfigured &&
                    <DescriptionListItem
                      title="PostgreSQL databases"
                      data={`${postgres.mainDatabase || 'main not set'} / ${postgres.logDatabase || 'log not set'} / ${postgres.cacheDatabase || 'cache not set'}`}
                    />
                }

                {
                  postgres.isConfigured &&
                    <DescriptionListItem
                      title="Credentials present"
                      data={`User: ${postgres.userConfigured ? 'configured' : 'missing'}, password: ${postgres.passwordConfigured ? 'configured' : 'missing'}`}
                    />
                }

                {
                  postgres.isReachable != null &&
                    <DescriptionListItem
                      title="Reachability check"
                      data={postgres.reachabilityMessage}
                    />
                }

                <DescriptionListItem
                  title="Backup"
                  data={databaseStatus.backupRecommendation}
                />

                <DescriptionListItem
                  title="Next step"
                  data={databaseStatus.nextStep}
                />

                {
                  databaseStatus.warnings?.length > 0 &&
                    <DescriptionListItem
                      title="Warnings"
                      data={
                        <ul className={styles.list}>
                          {
                            databaseStatus.warnings.map((warning, index) => {
                              return (
                                <li key={index}>
                                  {warning}
                                </li>
                              );
                            })
                          }
                        </ul>
                      }
                    />
                }

                <DescriptionListItem
                  title="Checklist"
                  data={
                    <ol className={styles.list}>
                      {
                        databaseStatus.checklist.map((item, index) => {
                          return (
                            <li key={index}>
                              {item}
                            </li>
                          );
                        })
                      }
                    </ol>
                  }
                />
              </DescriptionList>
            </FieldSet>
        }

        {
          (metadataServiceStatus || isFetchingMetadataServiceStatus || metadataServiceStatusError) &&
            <FieldSet legend="Metadata Service">
              {
                isFetchingMetadataServiceStatus &&
                  <DescriptionList className={styles.descriptionList}>
                    <DescriptionListItem
                      title="Status"
                      data="Checking metadata service and ReadAIrr update metadata..."
                    />
                  </DescriptionList>
              }

              {
                metadataServiceStatusError &&
                  <DescriptionList className={styles.descriptionList}>
                    <DescriptionListItem
                      title="Status"
                      data={
                        <Label kind={kinds.WARNING}>
                          Metadata service status unavailable
                        </Label>
                      }
                    />
                  </DescriptionList>
              }

              {
                metadataServiceStatus &&
                  <DescriptionList className={styles.descriptionList}>
                    <DescriptionListItem
                      title="Source"
                      data={`${metadataServiceStatus.sourceLabel} (${metadataServiceStatus.sourceType})`}
                    />

                    <DescriptionListItem
                      title="Endpoint"
                      data={metadataServiceStatus.serviceUrl}
                    />

                    <DescriptionListItem
                      title="Health"
                      data={
                        <Label kind={getMetadataReadinessKind(metadataServiceStatus.readinessState)}>
                          {metadataServiceStatus.readinessLabel}
                        </Label>
                      }
                    />

                    <DescriptionListItem
                      title="Health detail"
                      data={`${metadataServiceStatus.healthMessage}${metadataServiceStatus.statusCode ? ` (HTTP ${metadataServiceStatus.statusCode})` : ''}${metadataServiceStatus.responseTimeMs ? ` in ${metadataServiceStatus.responseTimeMs} ms` : ''}`}
                    />

                    {
                      metadataServiceStatus.healthDetail &&
                        <DescriptionListItem
                          title="Response detail"
                          data={metadataServiceStatus.healthDetail}
                        />
                    }

                    <DescriptionListItem
                      title="Update metadata"
                      data={`${metadataServiceStatus.updateEndpoint} on ${metadataServiceStatus.updateBranch}`}
                    />

                    <DescriptionListItem
                      title="App update"
                      data={
                        metadataServiceStatus.updateAvailable ?
                          `Update ${metadataServiceStatus.latestVersion} available from ${metadataServiceStatus.currentVersion}` :
                          metadataServiceStatus.updateCheckMessage
                      }
                    />

                    {
                      metadataServiceStatus.warnings?.length > 0 &&
                        <DescriptionListItem
                          title="Warnings"
                          data={
                            <ul className={styles.list}>
                              {
                                metadataServiceStatus.warnings.map((warning, index) => {
                                  return (
                                    <li key={index}>
                                      {warning}
                                    </li>
                                  );
                                })
                              }
                            </ul>
                          }
                        />
                    }

                    <DescriptionListItem
                      title="Checklist"
                      data={
                        <ol className={styles.list}>
                          {
                            metadataServiceStatus.checklist.map((item, index) => {
                              return (
                                <li key={index}>
                                  {item}
                                </li>
                              );
                            })
                          }
                        </ol>
                      }
                    />
                  </DescriptionList>
              }
            </FieldSet>
        }
      </>
    );
  }

}

About.propTypes = {
  version: PropTypes.string.isRequired,
  packageVersion: PropTypes.string,
  packageAuthor: PropTypes.string,
  isNetCore: PropTypes.bool.isRequired,
  runtimeVersion: PropTypes.string.isRequired,
  isDocker: PropTypes.bool.isRequired,
  databaseType: PropTypes.string.isRequired,
  databaseVersion: PropTypes.string.isRequired,
  migrationVersion: PropTypes.number.isRequired,
  appData: PropTypes.string.isRequired,
  startupPath: PropTypes.string.isRequired,
  mode: PropTypes.string.isRequired,
  startTime: PropTypes.string.isRequired,
  timeFormat: PropTypes.string.isRequired,
  longDateFormat: PropTypes.string.isRequired,
  databaseStatus: PropTypes.object,
  metadataServiceStatus: PropTypes.object,
  isFetchingMetadataServiceStatus: PropTypes.bool.isRequired,
  metadataServiceStatusError: PropTypes.object
};

export default About;
