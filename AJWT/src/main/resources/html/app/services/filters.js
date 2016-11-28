/**
 * Created by Jackyrul on 23.11.2016.
 */
(function () {
    'use strict';
    var app = angular.module('avalanchain');

    app.filter('reverse', function() {
        return function(items) {
            if(items)
                return items.slice().reverse();
        };
    });
    app.filter('pagination', function () {
        return function (input, start) {
            if (input) {
                start = +start; //parse to int
                return input.slice(start);
            }
            return [];
        }
    });
    app.filter('moment', function () {
        return function (date) {
            var dt =moment(date, "YYYYMMDD").fromNow();
            return dt;
        }
    });
})();