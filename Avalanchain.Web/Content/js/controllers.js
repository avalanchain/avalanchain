/**
 * avalanchain - Responsive Admin Theme
 * Copyright 2015 Webapplayers.com
 *
 */

/**
 * MainCtrl - controller
 */
function MainCtrl() {

    this.userName = 'User';
    this.helloText = 'Welcome in Avalanchain';
    this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

};


angular
    .module('avalanchain')
    .controller('MainCtrl', MainCtrl)