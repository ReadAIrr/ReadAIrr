import PropTypes from 'prop-types';
import React, { Component } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import styles from './OrganizePreviewRow.css';

class OrganizePreviewRow extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      id,
      isBlocked,
      onSelectedChange
    } = this.props;

    onSelectedChange({ id, value: !isBlocked });
  }

  //
  // Listeners

  onSelectedChange = ({ value, shiftKey }) => {
    const {
      id,
      onSelectedChange
    } = this.props;

    onSelectedChange({ id, value, shiftKey });
  };

  //
  // Render

  render() {
    const {
      id,
      existingPath,
      newPath,
      isBlocked,
      status,
      isSelected
    } = this.props;

    return (
      <div className={styles.row}>
        <CheckInput
          containerClassName={styles.selectedContainer}
          name={id.toString()}
          value={isBlocked ? false : isSelected}
          isDisabled={isBlocked}
          onChange={this.onSelectedChange}
        />

        <div>
          {
            isBlocked &&
              <div className={styles.warning}>
                <Icon
                  name={icons.WARNING}
                  kind={kinds.WARNING}
                />

                <span className={styles.path}>
                  {status}
                </span>
              </div>
          }

          <div>
            <Icon
              name={icons.SUBTRACT}
              kind={kinds.DANGER}
            />

            <span className={styles.path}>
              {existingPath}
            </span>
          </div>

          {
            !isBlocked &&
              <div>
                <Icon
                  name={icons.ADD}
                  kind={kinds.SUCCESS}
                />

                <span className={styles.path}>
                  {newPath}
                </span>
              </div>
          }
        </div>
      </div>
    );
  }
}

OrganizePreviewRow.propTypes = {
  id: PropTypes.number.isRequired,
  existingPath: PropTypes.string.isRequired,
  newPath: PropTypes.string.isRequired,
  isBlocked: PropTypes.bool.isRequired,
  status: PropTypes.string,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired
};

OrganizePreviewRow.defaultProps = {
  status: null
};

export default OrganizePreviewRow;
