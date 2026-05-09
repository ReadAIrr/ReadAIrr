import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import RelativeDateCellConnector from 'Components/Table/Cells/RelativeDateCellConnector';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { kinds } from 'Helpers/Props';
import titleCase from 'Utilities/String/titleCase';

function getKind(level) {
  switch (level) {
    case 'error':
      return kinds.DANGER;
    case 'warning':
      return kinds.WARNING;
    case 'notice':
      return kinds.INFO;
    default:
      return kinds.DEFAULT;
  }
}

function ActivityOverviewRow(props) {
  const {
    time,
    category,
    level,
    status,
    source,
    title,
    message,
    entity,
    columns
  } = props;

  return (
    <TableRow>
      {
        columns.map((column) => {
          const {
            name,
            isVisible
          } = column;

          if (!isVisible) {
            return null;
          }

          if (name === 'time') {
            return (
              <RelativeDateCellConnector
                key={name}
                date={time}
              />
            );
          }

          if (name === 'category') {
            return (
              <TableRowCell key={name}>
                {titleCase(category)}
              </TableRowCell>
            );
          }

          if (name === 'level') {
            return (
              <TableRowCell key={name}>
                <Label kind={getKind(level)}>
                  {titleCase(level)}
                </Label>
              </TableRowCell>
            );
          }

          if (name === 'status') {
            return (
              <TableRowCell key={name}>
                {titleCase(status)}
              </TableRowCell>
            );
          }

          if (name === 'source') {
            return (
              <TableRowCell key={name}>
                {source}
              </TableRowCell>
            );
          }

          if (name === 'title') {
            return (
              <TableRowCell key={name}>
                <div>{title}</div>
                {
                  entity &&
                    <div>{entity}</div>
                }
              </TableRowCell>
            );
          }

          if (name === 'message') {
            return (
              <TableRowCell key={name}>
                {message}
              </TableRowCell>
            );
          }

          return null;
        })
      }
    </TableRow>
  );
}

ActivityOverviewRow.propTypes = {
  time: PropTypes.string.isRequired,
  category: PropTypes.string.isRequired,
  level: PropTypes.string.isRequired,
  status: PropTypes.string,
  source: PropTypes.string,
  title: PropTypes.string,
  message: PropTypes.string,
  entity: PropTypes.string,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default ActivityOverviewRow;
