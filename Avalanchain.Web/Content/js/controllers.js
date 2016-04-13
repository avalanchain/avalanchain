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

function modalCtrl($scope, $uibModalInstance, dataservice, $rootScope) {
    var m = new Mnemonic(96);
    $scope.password = m.toWords().join(' ');;
    $scope.hexPass = m.toHex();
    $scope.guid = dataservice.createGuid();
    $scope.ok = function () {
        dataservice.newAccount().then(function (data) {
            $uibModalInstance.close();
            $rootScope.$emit('updateAccounts');
            //getAccounts();
        });
        
    };

    $scope.cancel = function () {
        $uibModalInstance.dismiss('cancel');
    };
};


angular
    .module('avalanchain')
    .controller('MainCtrl', MainCtrl)
    .controller('modalCtrl', MainCtrl);