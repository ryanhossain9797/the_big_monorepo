<?php

/*
|--------------------------------------------------------------------------
| Web Routes
|--------------------------------------------------------------------------
|
| Here is where you can register web routes for your application. These
| routes are loaded by the RouteServiceProvider within a group which
| contains the "web" middleware group. Now create something great!
|
*/

Route::get('/', function () {
    return view('welcome');
});

Auth::routes();

Route::get('/home', 'HomeController@index')->name('home');



Route::middleware(['auth'])->group(function(){
    Route::resource('/categories', 'CategoriesController');

    Route::resource('/tags', 'TagsController');

    Route::resource('/posts', 'PostsController');

    Route::get('trashed-posts', 'PostsController@trashed');
    Route::delete('trashed-posts', 'PostsController@trashed')->name('trashed-posts.index');
    Route::post('restore-post/{id}', 'PostsController@restore')->name('posts.restore');
});
