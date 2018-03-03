/**
 * avalanchain - Responsive Admin Theme
 *
 */

/**
 * MainCtrl - controller
 */
function MainCtrl() {

    this.userName = 'User';
    this.helloText = 'Welcome in avalanchain';
    this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

};

function modalCtrl($scope, $uibModalInstance, dataservice, $rootScope) {
    //var m = new Mnemonic(96);
    //$scope.password = m.toWords().join(' ');;
    //$scope.hexPass = m.toHex();
    //$scope.guid = dataservice.createGuid();
    $scope.modal = $rootScope.modal;
    $scope.ok = function () {
        $uibModalInstance.close();
        $scope.modal.ok();
    };
   

    $scope.cancel = function () {
        $uibModalInstance.dismiss('cancel');
    };
};


angular
    .module('avalanchain')
    .controller('MainCtrl', MainCtrl)
    .controller('modalCtrl', MainCtrl);